// This file defines the Node data structure, which is used to represent one node
// in a DecisionTree.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  /// <summary>
  /// One node in a decision tree.
  /// </summary>

  // NB We implement Nodes and their constituent IFeature and
  // IStatisticsAggregrator implementations using value types so as to
  // avoid the need for multiple objects to be allocated seperately on the
  // garbage collected heap - instead all the data associated with a tree can
  // be stored within a single, contiguous block of memory. This can
  // increase performance by (i) increasing spatial locality of reference
  // (and thus cache utilization) and (ii) decreasing the load on the .NET
  // memory manager.

  [Serializable]
  public struct Node<F, S>
    where F : IFeatureResponse
    where S : IStatisticsAggregator<S>
  {
    // NB Null nodes (i.e. uninitialized nodes corresponding to the bottom of
    // a tree branch for which training was terminated before maximum tree
    // depth) have bIsleaf==bIsDecisionNode_==false. Please see the IsSplit,
    // IsLeaf, and IsNull properties, below.

    bool bIsLeaf_;
    bool bIsSplit_;

    internal void InitializeLeaf(S trainingDataStatistics)
    {
      Feature = default(F);
      Threshold = 0.0f;
      bIsLeaf_ = true;
      bIsSplit_ = false;
      TrainingDataStatistics = trainingDataStatistics.DeepClone();
    }

    internal void InitializeSplit(F feature, float threshold, S trainingDataStatistics)
    {
      bIsLeaf_ = false;
      bIsSplit_ = true;
      Feature = feature;
      Threshold = threshold;
      TrainingDataStatistics = trainingDataStatistics.DeepClone();
    }

    /// <summary>
    /// Along with an associated threshold, defines the weak learner
    /// associated with a split node. This member is only valid for
    /// split nodes.
    /// </summary>
    public F Feature;

    /// <summary>
    /// Along with an associated feature, defines the weak learner
    /// associated with a split node. This member is only valid fro
    /// split nodes.
    /// </summary>
    public float Threshold;

    /// <summary>
    /// Statistics computed over training data points that reached this
    /// node.
    /// </summary>

    // NB We store training data statistics for all nodes, including
    // decision nodes - this way we can prune the tree subsequent to
    // training.

    public S TrainingDataStatistics;

    /// <summary>
    /// Is this a decision node, i.e. a node with an associated weak learner
    /// and child nodes?
    /// </summary>
    public bool IsSplit { get { return bIsSplit_ && !bIsLeaf_; } }

    /// <summary>
    /// Is this a leaf node, i.e. a node with no associated weak learner
    /// or child nodes?
    /// </summary>
    public bool IsLeaf { get { return bIsLeaf_ && !bIsSplit_; } }

    /// <summary>
    /// Is this an uninitialized node (corresponding to the bottom of
    // a tree branch for which training was terminated before maximum tree
    // depth or a not-yet-trained node)?
    /// </summary>
    public bool IsNull { get { return !bIsLeaf_ && !bIsSplit_; } }
  }
}
