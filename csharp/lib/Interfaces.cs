// This file defines interfaces used during decision forest training and
// evaluation. THese interfaces are intended to be implemented within client code.

using System;
using System.Collections.Generic;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  /// <summary>
  /// A collection of data points used for forest training or evaluation.
  /// Concrete implementations supplied by client code will collaborate
  /// with concrete IFeature and IStatisticsAggregator implementations for
  /// feature evaluation and statistics aggregation over data points.
  /// </summary>
  public interface IDataPointCollection
  {
    int Count();
  }

  /// <summary>
  /// Features compute (single precision) response values for data points. A
  /// 'weak learner' comprises a feature and an associated decision threshold.
  /// </summary>
  public interface IFeatureResponse
  {
    /// <summary>
    /// Computes the response for the specified data point.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="dataIndex">The index of the data point to be evaluated.</param>
    /// <returns>A single precision response value.</returns>
    float GetResponse(IDataPointCollection data, int dataIndex);
  };

  /// <summary>
  /// Used during forest training to aggregate statistics over sets of data
  /// points. The precise nature of the statistic to be aggregated is up to
  /// the caller. Common statistics include histograms over class labels (for
  /// classification problems) and sum and sum of squares (for regression
  /// problems).
  /// </summary>
  public interface IStatisticsAggregator<S>
  {
    /// <summary>
    /// Called by the training framework to reset sample statistics. Allows
    /// IStatisticsAggregrator instances to be reused in the interests of
    /// avoiding unnecessary memeory allocations.
    /// </summary>
    void Clear();

    /// <summary>
    /// Update statistics with one additional data point.
    /// </summary>
    /// <param name="data">The data point collection.</param>
    /// <param name="dataIndex">The index of the data point.</param>
    void Aggregate(IDataPointCollection data, int index);

    /// <summary>
    /// Combine two sets of statistics.
    /// </summary>
    /// <param name="i">The statistics to be combined.</param>
    void Aggregate(S i);

    /// <summary>
    /// Called by the training framework to make a clone of the sample statistics to be stored in the leaf of a tree
    /// </summary>
    /// <returns></returns>
    S DeepClone();
  }

  /// <summary>
  /// An abstract representation of a decision forest training problem that
  /// intended to be implemented within client code. Instances of this
  /// interface are used by the training framework to instantiate new
  /// IFeature and IStatisticsAggregator instances, to compute
  /// information gain, and to decide when to terminate training of a
  /// particular tree branch.
  /// </summary>
  public interface ITrainingContext<F, S>
    where F : IFeatureResponse
    where S : IStatisticsAggregator<S>
  {
    /// <summary>
    /// Called by the training framework to generate a new random feature.
    /// Concrete implementations must return a new feature.
    /// </summary>
    F GetRandomFeature(Random random);

    /// <summary> 
    /// Called by the training framework to get an instance of
    /// a concrete IStatisticsAggregator implementation.
    /// </summary>
    S GetStatisticsAggregator();

    /// <summary>
    /// Called by the training framework to compute the gain over a given
    /// binary partition of a set of samples.
    /// </summary>
    /// <param name="parent">Statistics aggregated over the complete set of samples.</param>
    /// <param name="leftChild">Statistics aggregated over the left hand partition.</param>
    /// <param name="rightChild">Statistics aggregated over the right hand partition.</param>
    /// <returns>A measure of gain, e.g. entropy gain in bits.</returns>
    double ComputeInformationGain(S parent, S leftChild, S rightChild);

    /// <summary>
    /// Called by the training framework to determine whether training
    /// should terminate for this branch.  Concrete implementations must
    /// determine whether to terminate training based on statistics
    /// aggregated over the left and right hand sides of the best
    /// binary partition found.
    /// </summary>
    /// <param name="parent">Statistics aggregated over the complete set of samples.</param>
    /// <param name="leftChild">Statistics aggregated over the left hand partition.</param>
    /// <param name="rightChild">Statistics aggregated over the right hand partition.</param>
    /// <param name="rightChild">Gain computed for this binary partition
    /// within a previous call to ISampleCollection.ComputeGain().</param>
    /// <returns>True if training should be terminated, false otherwise.</returns>
    bool ShouldTerminate(S parent, S leftChild, S rightChild, double gain);
  }
}