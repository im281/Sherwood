// This file defines the ParallelForestTrainer and ParallelTreeTrainer classes,
// which are responsible for creating new Tree instances by learning from training
// data. These classes have almost identical interfaces to ForestTrainer and
// TreeTrainer, but allow candidate feature evaluation to be shared over a
// specified maximum number of threads.

using System;
using System.Collections.Generic;

using System.Threading;
using System.Threading.Tasks;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  /// <summary>
  /// Learns new decision forests from training data.
  /// </summary>
  public class ParallelForestTrainer<F, S>
    where F : IFeatureResponse
    where S : IStatisticsAggregator<S>
  {
    /// <summary>
    /// Train a new decision forest given some training data and a training
    /// problem described by an instance of the ITrainingContext interface.
    /// </summary>
    /// <param name="random">Random number generator.</param>
    /// <param name="parameters">Training parameters.</param>
    /// <param name="maxThreads">The maximum number of threads to use.</param>
    /// <param name="context">An ITrainingContext instance describing
    /// the training problem, e.g. classification, density estimation, etc. </param>
    /// <param name="data">The training data.</param>
    /// <returns>A new decision forest.</returns>
    public static Forest<F, S> TrainForest(
      Random random,
      TrainingParameters parameters,
      ITrainingContext<F, S> context,
      int maxThreads,
      IDataPointCollection data,
      ProgressWriter progress = null)
    {
      if (progress == null)
        progress = new ProgressWriter(parameters.Verbose?Verbosity.Verbose:Verbosity.Interest, Console.Out);

      Forest<F, S> forest = new Forest<F, S>();

      for (int t = 0; t < parameters.NumberOfTrees; t++)
      {
        progress.Write(Verbosity.Interest, "\rTraining tree {0}...", t);

        Tree<F, S> tree = ParallelTreeTrainer<F, S>.TrainTree(random, context, parameters, maxThreads, data, progress);
        forest.AddTree(tree);
      }
      progress.WriteLine(Verbosity.Interest, "\rTrained {0} trees.         ", parameters.NumberOfTrees);

      return forest;
    }
  }

  /// <summary>
  /// Used for multi-threaded decision tree training. Candidate feature
  /// response function evaluation is distributed over multiple threads.
  /// </summary>
  public class ParallelTreeTrainer<F, S>
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
    /// <param name="maxThreads">The maximum number of threads to use.</param>
    /// <param name="data">The training data.</param>
    /// <returns>A new decision tree.</returns>
    static public Tree<F, S> TrainTree(
      Random random,
      ITrainingContext<F, S> context,
      TrainingParameters parameters,
      int maxThreads,
      IDataPointCollection data,
      ProgressWriter progress = null)
    {
      if (progress == null)
        progress = new ProgressWriter(Verbosity.Interest, Console.Out);

      ParallelTreeTrainingOperation<F, S> trainingOperation = new ParallelTreeTrainingOperation<F, S>(
        random, context, parameters, maxThreads, data, progress);

      Tree<F, S> tree = new Tree<F, S>(parameters.MaxDecisionLevels);

      trainingOperation.TrainNodesRecurse(tree.nodes_, 0, 0, data.Count(), 0);  // will recurse until termination criterion is met

      tree.CheckValid();

      return tree;
    }
  };

  /// <summary>
  /// A decision tree training operation in which candidate feature response
  /// function evaluation is distributed over multiple threads - used
  /// internally within TreeTrainer to encapsulate the training a single tree.
  /// </summary>
  class ParallelTreeTrainingOperation<F, S>
    where F : IFeatureResponse
    where S : IStatisticsAggregator<S>
  {
    private IDataPointCollection data_;
    private ITrainingContext<F, S> trainingContext_;

    private TrainingParameters parameters_;
    int maxThreads_;

    private Random random_;

    ProgressWriter progress_;

    // Thread-global storage
    public float[] responses_;
    private int[] indices_;
    public S parentStatistics_, leftChildStatistics_, rightChildStatistics_;

    // Thread-local storage
    class ThreadLocalData
    {
      public ThreadLocalData(Random random, ITrainingContext<F, S> trainingContext_, TrainingParameters parameters, IDataPointCollection data)
      {
        parentStatistics_ = trainingContext_.GetStatisticsAggregator();

        leftChildStatistics_ = trainingContext_.GetStatisticsAggregator();
        rightChildStatistics_ = trainingContext_.GetStatisticsAggregator();

        partitionStatistics_ = new S[parameters.NumberOfCandidateThresholdsPerFeature + 1];
        for (int i = 0; i < parameters.NumberOfCandidateThresholdsPerFeature + 1; i++)
          partitionStatistics_[i] = trainingContext_.GetStatisticsAggregator();

        responses_ = new float[data.Count()];

        random_ = new Random(random.Next());
      }

      public void Clear()
      {
        maxGain = 0.0;
        bestFeature = default(F);
        bestThreshold = 0.0f;
      }

      public double maxGain = 0.0;
      public F bestFeature = default(F);
      public float bestThreshold = 0.0f;

      public S parentStatistics_, leftChildStatistics_, rightChildStatistics_;

      public float[] responses_;

      public float[] thresholds = null;
      public S[] partitionStatistics_;

      public Random random_;
    }
    ThreadLocalData[] threadLocals_;

    public ParallelTreeTrainingOperation(
      Random random,
      ITrainingContext<F, S> trainingContext,
      TrainingParameters parameters,
      int maxThreads,
      IDataPointCollection data,
      ProgressWriter progress)
    {
      data_ = data;
      trainingContext_ = trainingContext;
      parameters_ = parameters;

      maxThreads_ = maxThreads;

      random_ = random;

      progress_ = progress;

      parentStatistics_ = trainingContext_.GetStatisticsAggregator();

      leftChildStatistics_ = trainingContext_.GetStatisticsAggregator();
      rightChildStatistics_ = trainingContext_.GetStatisticsAggregator();

      responses_ = new float[data.Count()];

      indices_ = new int[data.Count()];
      for (int i = 0; i < indices_.Length; i++)
        indices_[i] = i;

      threadLocals_ = new ThreadLocalData[maxThreads_];
      for (int threadIndex = 0; threadIndex < maxThreads_; threadIndex++)
        threadLocals_[threadIndex] = new ThreadLocalData(random_, trainingContext_, parameters_, data_);
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

      // Copy parent statistics to thread local storage in case client
      // IStatisticsAggregator implementations are not reentrant.
      for (int t = 0; t < maxThreads_; t++)
        threadLocals_[t].parentStatistics_ = parentStatistics_.DeepClone();

      if (nodeIndex >= nodes.Length / 2) // this is a leaf node, nothing else to do
      {
        nodes[nodeIndex].InitializeLeaf(parentStatistics_);
        progress_.WriteLine(Verbosity.Verbose, "Terminating at max depth.");

        return;
      }

      // Iterate over threads
      Parallel.For(0, maxThreads_, new Action<int>(threadIndex =>
      {
        ThreadLocalData tl = threadLocals_[threadIndex]; // shorthand
        tl.Clear();

        // Range of indices of candidate feature evaluated in this thread
        // (if the number of candidate features is not a multiple of the
        // number of threads, some threads may evaluate one more feature
        // than others).
        int f1 = (int)(Math.Round(parameters_.NumberOfCandidateFeatures * (double)threadIndex / (double)maxThreads_));
        int f2 = (int)(Math.Round(parameters_.NumberOfCandidateFeatures * (double)(threadIndex + 1) / (double)maxThreads_));

        // Iterate over candidate features
        for (int f = f1; f < f2; f++)
        {
          F feature = trainingContext_.GetRandomFeature(tl.random_);

          for (int b = 0; b < parameters_.NumberOfCandidateThresholdsPerFeature + 1; b++)
            threadLocals_[threadIndex].partitionStatistics_[b].Clear(); // reset statistics

          // Compute feature response per sample at this node
          for (int i = i0; i < i1; i++)
            tl.responses_[i] = feature.GetResponse(data_, indices_[i]);

          int nThresholds;
          if ((nThresholds = ParallelTreeTrainingOperation<F, S>.ChooseCandidateThresholds(
              tl.random_,
              indices_, i0, i1,
              tl.responses_,
              parameters_.NumberOfCandidateThresholdsPerFeature,
              ref tl.thresholds)) == 0)
            continue; // failed to find meaningful candidate thresholds for this feature

          // Aggregate statistics over sample partitions
          for (int i = i0; i < i1; i++)
          {
            // Slightly faster than List<float>.BinarySearch() for < O(100) thresholds
            int b = 0;
            while (b < nThresholds && tl.responses_[i] >= tl.thresholds[b])
              b++;

            tl.partitionStatistics_[b].Aggregate(data_, indices_[i]);
          }

          for (int t = 0; t < nThresholds; t++)
          {
            tl.leftChildStatistics_.Clear();
            tl.rightChildStatistics_.Clear();
            for (int p = 0; p < nThresholds + 1 /*i.e. nBins*/; p++)
            {
              if (p <= t)
                tl.leftChildStatistics_.Aggregate(tl.partitionStatistics_[p]);
              else
                tl.rightChildStatistics_.Aggregate(tl.partitionStatistics_[p]);
            }

            // Compute gain over sample partitions
            double gain = trainingContext_.ComputeInformationGain(tl.parentStatistics_, tl.leftChildStatistics_, tl.rightChildStatistics_);

            if (gain >= tl.maxGain)
            {
              tl.maxGain = gain;
              tl.bestFeature = feature;
              tl.bestThreshold = tl.thresholds[t];
            }
          }
        }
      }));

      double maxGain = 0.0;
      F bestFeature = default(F);
      float bestThreshold = 0.0f;

      // Now merge over threads
      for (int threadIndex = 0; threadIndex < maxThreads_; threadIndex++)
      {
        ThreadLocalData tl = threadLocals_[threadIndex];
        if (tl.maxGain > maxGain)
        {
          maxGain = tl.maxGain;
          bestFeature = tl.bestFeature;
          bestThreshold = tl.bestThreshold;
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

      // Otherwise this is a new split node, recurse for children.
      nodes[nodeIndex].InitializeSplit(
        bestFeature,
        bestThreshold,
        parentStatistics_.DeepClone());

      // Now do partition sort - any sample with response greater goes left, otherwise right
      int ii = Tree<F, S>.Partition(responses_, indices_, i0, i1, bestThreshold);

      System.Diagnostics.Debug.Assert(ii >= i0 && i1 >= ii);

      progress_.WriteLine(Verbosity.Verbose, "{0} (threshold = {1:G3}, gain = {2:G3}).", bestFeature.ToString(), bestThreshold, maxGain);

      TrainNodesRecurse(nodes, nodeIndex * 2 + 1, i0, ii, recurseDepth + 1);
      TrainNodesRecurse(nodes, nodeIndex * 2 + 2, ii, i1, recurseDepth + 1);
    }

    internal static int ChooseCandidateThresholds(
        Random randomNumberGenerator_,
        int[] dataIndices, int i0, int i1,
        float[] responses,
        int nWanted,
        ref float[] thresholds)
    {
      if (thresholds == null || thresholds.Length < nWanted + 1)
        thresholds = new float[nWanted + 1]; // lazy allocation

      // Form approximate quantiles by sorting a random draw of response values
      int nThresholds;
      float[] quantiles = thresholds; // reuse same block of memory to avoid allocation
      if (i1 - i0 > nWanted)
      {
        nThresholds = nWanted;
        for (int i = 0; i < nThresholds + 1; i++)
          quantiles[i] = responses[randomNumberGenerator_.Next(i0, i1)]; // sample randomly from all responses
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
        thresholds[i] = quantiles[i] + (float)(randomNumberGenerator_.NextDouble() * (quantiles[i + 1] - quantiles[i]));

      return nThresholds;
    }
  }
}