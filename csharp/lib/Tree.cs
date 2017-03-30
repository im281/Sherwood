// This file defines the Tree class, which is used to represent decision trees.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  /// <summary>
  /// A decision tree comprising multiple nodes.
  /// </summary>
  [Serializable]
  public class Tree<F, S>
    where F : IFeatureResponse
    where S : IStatisticsAggregator<S>
  {
    internal Node<F, S>[] nodes_;

    internal Tree(int decisionLevels)
    {
      if(decisionLevels<0)
        throw new Exception("Tree can't have less than 0 decision levels.");

      if(decisionLevels>19)
        throw new Exception("Tree can't have more than 19 decision levels.");

      // This full allocation of node storage may be wasteful of memory
      // if trees are unbalanced but is efficient otherwise. Because child
      // node indices can determined directly from the parent node's index
      // it isn't necessary to store parent-child references within the
      // nodes.
      nodes_ = new Node<F, S>[(1 << (decisionLevels + 1)) - 1];
    }

    /// <summary>
    /// Apply the decision tree to a collection of test data points.
    /// </summary>
    /// <param name="data">The test data.</param>
    /// <returns>An array of leaf node indices per data point.</returns>
    public int[] Apply(IDataPointCollection data)
    {
      CheckValid();

      int[] leafNodeIndices = new int[data.Count()]; // of leaf node reached per data point

      // Allocate temporary storage for data point indices and response values
      int[] dataIndices_ = new int[data.Count()];
      for (int i = 0; i < data.Count(); i++)
        dataIndices_[i] = i;

      float[] responses_ = new float[data.Count()];

      ApplyNode(0, data, dataIndices_, 0, data.Count(), leafNodeIndices, responses_);

      return leafNodeIndices;
    }

    /// <summary>
    /// The number of nodes in the tree, including decision, leaf, and null nodes.
    /// </summary>
    public int NodeCount
    {
      get
      {
        return nodes_.Length;
      }
    }

    /// <summary>
    /// Return the specified tree node.
    /// </summary>
    /// <param name="index">A zero-based node index.</param>
    /// <returns>A copy of the node.</returns>
    public Node<F, S> GetNode(int index)
    {
      return nodes_[index];
    }

    public void SetNode(int index, Node<F, S> node)
    {
      nodes_[index] = node;
    }

    internal static int Partition(float[] keys, int[] values, int i0, int i1, float threshold)
    {
      Debug.Assert(i1 > i0, "Past-the-end element index must be greater than start element index.");

      int i = i0;     // index of first element
      int j = i1 - 1; // index of last element

      while (i != j)
      {
        if (keys[i] >= threshold)
        {
          // Swap keys[i] with keys[j]
          float key = keys[i];
          int value = values[i];

          keys[i] = keys[j];
          values[i] = values[j];

          keys[j] = key;
          values[j] = value;

          j--;
        }
        else
        {
          i++;
        }
      }

      return keys[i] >= threshold ? i : i + 1;
    }

    public void CheckValid()
    {
      if (NodeCount == 0)
        throw new Exception("Valid tree must have at least one node.");

      if (GetNode(0).IsNull == true)
        throw new Exception("A valid tree must have non-null root node.");

      CheckValidRecurse(0, false);
    }

    void CheckValidRecurse(int index, bool bHaveReachedLeaf)
    {
      if (bHaveReachedLeaf == false && GetNode(index).IsLeaf)
      {
        // First time I have encountered a leaf node
        bHaveReachedLeaf = true;
      }
      else
      {
        if (bHaveReachedLeaf)
        {
          // Have encountered a leaf node already, this node had better be null
          if (GetNode(index).IsNull == false)
            throw new Exception("Valid tree must have all descendents of leaf nodes set as null nodes.");
        }
        else
        {
          // Have not encountered a leaf node yet, this node had better be a split node
          if (GetNode(index).IsSplit == false)
            throw new Exception("Valid tree must have all antecents of leaf nodes set as split nodes.");
        }
      }

      if (index >= (NodeCount - 1) / 2)
      {
        // At maximum depth, this node had better be a leaf
        if (bHaveReachedLeaf == false)
          throw new Exception("Valid tree must have all branches terminated by leaf nodes.");
      }
      else
      {
        CheckValidRecurse(2 * index + 1, bHaveReachedLeaf);
        CheckValidRecurse(2 * index + 2, bHaveReachedLeaf);
      }
    }

    public static string GetPrettyPrintPrefix(int nodeIndex)
    {
      string prefix = nodeIndex > 0 ? (nodeIndex % 2 == 1 ? "├─O " : "└─O ") : "O ";
      for (int l = (nodeIndex - 1) / 2; l > 0; l = (l - 1) / 2)
        prefix = (l % 2 == 1 ? "│ " : "  ") + prefix;
      return prefix;
    }

    private void ApplyNode(
        int nodeIndex,
        IDataPointCollection data,
        int[] dataIndices,
        int i0,
        int i1,
        int[] leafNodeIndices,
        float[] responses_)
    {
      System.Diagnostics.Debug.Assert(nodes_[nodeIndex].IsNull == false);

      Node<F, S> node = nodes_[nodeIndex];

      if (node.IsLeaf)
      {
        for (int i = i0; i < i1; i++)
          leafNodeIndices[dataIndices[i]] = nodeIndex;
        return;
      }

      if (i0 == i1)   // No samples left
        return;

      for (int i = i0; i < i1; i++)
        responses_[i] = node.Feature.GetResponse(data, dataIndices[i]);

      int ii = Partition(responses_, dataIndices, i0, i1, node.Threshold);

      // Recurse for child nodes.
      ApplyNode(nodeIndex * 2 + 1, data, dataIndices, i0, ii, leafNodeIndices, responses_);
      ApplyNode(nodeIndex * 2 + 2, data, dataIndices, ii, i1, leafNodeIndices, responses_);
    }
  }
}
