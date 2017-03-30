// This file defines the ForestTraininer and TreeTrainer classes, which are
// responsible for creating new DecisionForest instances by learning from
// training data. Please see also ParallelForestTrainer.cs.

using System;
using System.Collections.Generic;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  /// <summary>
  /// Decision tree training parameters,
  /// </summary>
  public class TrainingParameters
  {
    public int NumberOfTrees = 6;
    public int NumberOfCandidateFeatures = 100;
    public int NumberOfCandidateThresholdsPerFeature = 10;
    public int MaxDecisionLevels = 5;
    public bool Verbose = false;
  }

  /// <summary>
  /// Learns new decision forests from training data.
  /// </summary>
  public class ForestTrainer<F, S>
    where F : IFeatureResponse
    where S : IStatisticsAggregator<S>
  {
    /// <summary>
    /// Train a new decision forest given some training data and a training
    /// problem described by an instance of the ITrainingContext interface.
    /// </summary>
    /// <param name="random">Random number generator.</param>
    /// <param name="parameters">Training parameters.</param>
    /// <param name="context">An ITrainingContext instance describing
    /// the training problem, e.g. classification, density estimation, etc. </param>
    /// <param name="data">The training data.</param>
    /// <returns>A new decision forest.</returns>
    public static Forest<F, S> TrainForest(
      Random random_,
      TrainingParameters parameters,
      ITrainingContext<F, S> context,
      IDataPointCollection data,
      ProgressWriter progress = null)
    {
      if (progress == null)
        progress = new ProgressWriter(parameters.Verbose?Verbosity.Verbose:Verbosity.Interest, Console.Out);

      Forest<F, S> forest = new Forest<F, S>();

      for (int t = 0; t < parameters.NumberOfTrees; t++)
      {
        progress.Write(Verbosity.Interest, "\rTraining tree {0}...", t);

        Tree<F, S> tree = TreeTrainer<F, S>.TrainTree(random_, context, parameters, data, progress);
        forest.AddTree(tree);
      }
      progress.WriteLine(Verbosity.Interest, "\rTrained {0} trees.         ", parameters.NumberOfTrees);

      return forest;
    }
  }

  /// <summary>
  /// A decision tree training operation - used internally within TreeTrainer
  /// to represent the operation of training a single tree.
  /// </summary>
  class TreeTrainingOperation<F, S>
    where F : IFeatureResponse
    where S : IStatisticsAggregator<S>
  {
    private IDataPointCollection data_;
    private ITrainingContext<F, S> trainingContext_;

    private TrainingParameters parameters_;

    private Random random_;

    private ProgressWriter progress_;

    private int[] indices_;

    private float[] responses_;

    private S parentStatistics_, leftChildStatistics_, rightChildStatistics_;
    private S[] partitionStatistics_;

    public TreeTrainingOperation(
      Random randomNumberGenerator,
      ITrainingContext<F, S> trainingContext,
      TrainingParameters parameters,
      IDataPointCollection data,
      ProgressWriter progress)
    {
      data_ = data;
      trainingContext_ = trainingContext;
      parameters_ = parameters;

      random_ = randomNumberGenerator;
      progress_ = progress;

      indices_ = new int[data.Count()];
      for (int i = 0; i < indices_.Length; i++)
        indices_[i] = i;

      responses_ = new float[data.Count()];

      parentStatistics_ = trainingContext_.GetStatisticsAggregator();

      leftChildStatistics_ = trainingContext_.GetStatisticsAggregator();
      rightChildStatistics_ = trainingContext_.GetStatisticsAggregator();

      partitionStatistics_ = new S[parameters.NumberOfCandidateThresholdsPerFeature + 1];
      for (int i = 0; i < parameters.NumberOfCandidateThresholdsPerFeature + 1; i++)
        partitionStatistics_[i] = trainingContext_.GetStatisticsAggregator();
    }

    public void TrainNodesRecurse(Node<F, S>[] nodes, int nodeIndex, int i0, int i1, int recurseDepth)
    {
      System.Diagnostics.Debug.Assert(nodeIndex < nodes.Length);

      nodes[nodeIndex] = new Node<F, S>();

      progress_.Write(Verbosity.Verbose, "{0}{1}: ", Tree<F, S>.GetPrettyPrintPrefix(nodeIndex), i1 - i0);

      // First aggregate statistics over the samples at the parent node
      parentStatistics_.Clear();
      for (int i = i0; i < i1; i++)
        parentStatistics_.Aggregate(data_, indices_[i]);

      if (nodeIndex >= nodes.Length / 2) // this is a leaf node, nothing else to do
      {
        nodes[nodeIndex].InitializeLeaf(parentStatistics_);
        progress_.WriteLine(Verbosity.Verbose, "Terminating at max depth.");
        return;
      }

      double maxGain = 0.0;
      F bestFeature = default(F);
      float bestThreshold = 0.0f;

      // Iterate over candidate features
      float[] thresholds = null;
      for (int f = 0; f < parameters_.NumberOfCandidateFeatures; f++)
      {
        F feature = trainingContext_.GetRandomFeature(random_);

        for (int b = 0; b < parameters_.NumberOfCandidateThresholdsPerFeature + 1; b++)
          partitionStatistics_[b].Clear(); // reset statistics

        // Compute feature response per samples at this node
        for (int i = i0; i < i1; i++)
          responses_[i] = feature.GetResponse(data_, indices_[i]);

        int nThresholds;
        if ((nThresholds = ChooseCandidateThresholds(indices_, i0, i1, responses_, ref thresholds)) == 0)
          continue;

        // Aggregate statistics over sample partitions
        for (int i = i0; i < i1; i++)
        {
          // Slightly faster than List<float>.BinarySearch() for fewer than 100 thresholds
          int b = 0;
          while (b < nThresholds && responses_[i] >= thresholds[b])
            b++;

          partitionStatistics_[b].Aggregate(data_, indices_[i]);
        }

        for (int t = 0; t < nThresholds; t++)
        {
          leftChildStatistics_.Clear();
          rightChildStatistics_.Clear();
          for (int p = 0; p < nThresholds + 1 /*i.e. nBins*/; p++)
          {
            if (p <= t)
              leftChildStatistics_.Aggregate(partitionStatistics_[p]);
            else
              rightChildStatistics_.Aggregate(partitionStatistics_[p]);
          }

          // Compute gain over sample partitions
          double gain = trainingContext_.ComputeInformationGain(parentStatistics_, leftChildStatistics_, rightChildStatistics_);

          if (gain >= maxGain)
          {
            maxGain = gain;
            bestFeature = feature;
            bestThreshold = thresholds[t];
          }
        }
      }

      if (maxGain == 0.0)
      {
        nodes[nodeIndex].InitializeLeaf(parentStatistics_);
        progress_.WriteLine(Verbosity.Verbose, "Terminating with no beneficial split found.");
        return;
      }

      // Now reorder the data point indices using the winning feature and thresholds.
      // Also recompute child node statistics so the client can decide whether
      // to terminate training of this branch.
      leftChildStatistics_.Clear();
      rightChildStatistics_.Clear();

      for (int i = i0; i < i1; i++)
      {
        responses_[i] = bestFeature.GetResponse(data_, indices_[i]);
        if (responses_[i] < bestThreshold)
          leftChildStatistics_.Aggregate(data_, indices_[i]);
        else
          rightChildStatistics_.Aggregate(data_, indices_[i]);
      }

      if (trainingContext_.ShouldTerminate(parentStatistics_, leftChildStatistics_, rightChildStatistics_, maxGain))
      {
        nodes[nodeIndex].InitializeLeaf(parentStatistics_);
        progress_.WriteLine(Verbosity.Verbose, "Terminating because supplied termination criterion is satisfied.");
        return;
      }

      nodes[nodeIndex].InitializeSplit(bestFeature, bestThreshold, parentStatistics_);

      // Now do partition sort - any sample with response greater goes left, otherwise right
      int ii = Tree<F, S>.Partition(responses_, indices_, i0, i1, bestThreshold);

      System.Diagnostics.Debug.Assert(ii >= i0 && i1 >= ii);

      progress_.WriteLine(Verbosity.Verbose, "{0} (threshold = {1:G3}, gain = {2:G3}).", bestFeature.ToString(), bestThreshold, maxGain);

      // Otherwise this is a new decision node, recurse for children.
      TrainNodesRecurse(nodes, nodeIndex * 2 + 1, i0, ii, recurseDepth + 1);
      TrainNodesRecurse(nodes, nodeIndex * 2 + 2, ii, i1, recurseDepth + 1);
    }

    private int ChooseCandidateThresholds(int[] dataIndices, int i0, int i1, float[] responses, ref float[] thresholds)
    {
      if (thresholds == null || thresholds.Length < parameters_.NumberOfCandidateThresholdsPerFeature + 1)
        thresholds = new float[parameters_.NumberOfCandidateThresholdsPerFeature + 1]; // lazy allocation

      // Form approximate quantiles by sorting a random draw of response values
      int nThresholds;
      float[] quantiles = thresholds; // reuse same block of memory to avoid allocation
      if (i1 - i0 > parameters_.NumberOfCandidateThresholdsPerFeature)
      {
        nThresholds = parameters_.NumberOfCandidateThresholdsPerFeature;
        for (int i = 0; i < nThresholds + 1; i++)
          quantiles[i] = responses[random_.Next(i0, i1)]; // sample randomly from all responses
      }
      else
      {
        nThresholds = i1 - i0 - 1;
        Array.Copy(responses, i0, quantiles, 0, i1 - i0);
      }
      Array.Sort(quantiles);

      if (quantiles[0] == quantiles[nThresholds])
        return 0;   // all sampled response values were the same

      // We from n candidate thresholds by sampling in between n+1 approximate quantiles
      for (int i = 0; i < nThresholds; i++)
        thresholds[i] = quantiles[i] + (float)(random_.NextDouble() * (quantiles[i + 1] - quantiles[i]));

      return nThresholds;
    }
  }

  /// <summary>
  /// Used to train decision trees.
  /// </summary>
  public class TreeTrainer<F, S>
    where F : IFeatureResponse
    where S : IStatisticsAggregator<S>
  {
    /// <summary>
    /// Train a new decision tree given some training data and a training
    /// problem described by an ITrainingContext instance.
    /// </summary>
    /// <param name="random">The single random number generator.</param>
    /// <param name="progress">Progress reporting target.</param>
    /// <param name="context">The ITrainingContext instance by which
    /// the training framework interacts with the training data.
    /// Implemented within client code.</param>
    /// <param name="parameters">Training parameters.</param>
    /// <param name="data">The training data.</param>
    /// <returns>A new decision tree.</returns>
    static public Tree<F, S> TrainTree(
      Random random,
      ITrainingContext<F, S> context,
      TrainingParameters parameters,
      IDataPointCollection data,
      ProgressWriter progress = null)
    {
      if (progress == null)
        progress = new ProgressWriter(Verbosity.Interest, Console.Out);
      TreeTrainingOperation<F, S> trainingOperation = new TreeTrainingOperation<F, S>(
        random, context, parameters, data, progress);

      Tree<F, S> tree = new Tree<F, S>(parameters.MaxDecisionLevels);

      progress.WriteLine(Verbosity.Verbose, "");

      trainingOperation.TrainNodesRecurse(tree.nodes_, 0, 0, data.Count(), 0);  // will recurse until termination criterion is met

      progress.WriteLine(Verbosity.Verbose, "");

      tree.CheckValid();

      return tree;
    }
  };
}