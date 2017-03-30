// This file declares the Forest class, which is used to represent forests
// of decisions trees.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  /// <summary>
  /// A decision forest, i.e. a collection of decision trees.
  /// </summary>
  [Serializable]
  public class Forest<F, S>
    where F : IFeatureResponse
    where S : IStatisticsAggregator<S>
  {
    internal List<Tree<F, S>> trees_ = new List<Tree<F, S>>();

    internal void AddTree(Tree<F, S> tree)
    {
      tree.CheckValid();

      trees_.Add(tree);
    }

    /// <summary>
    /// Deserialize a forest from a file.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The forest.</returns>
    public static Forest<F, S> FromFile(string path)
    {
      Forest<F, S> forest;
      using (Stream stream = File.Open(path, FileMode.Open))
      {
        forest = FromStream(stream);
      }

      return forest;
    }

    /// <summary>
    /// Deserialize a forest from a binary stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <returns></returns>
    public static Forest<F, S> FromStream(Stream stream)
    {
      BinaryFormatter formatter = new BinaryFormatter();

      Forest<F, S> forest = (Forest<F, S>)formatter.Deserialize(stream);

      for (int f = 0; f < forest.TreeCount; f++)
        forest.GetTree(f).CheckValid();

      return forest;
    }

    /// <summary>
    /// Serialize the forest to file.
    /// </summary>
    /// <param name="path">The file path.</param>
    public void Serialize(string path)
    {
      using (Stream stream = File.Open(path, FileMode.Create))
      {
        this.Serialize(stream);
      }
    }

    /// <summary>
    /// Serialize the forest a binary stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    public void Serialize(Stream stream)
    {
      BinaryFormatter formatter = new BinaryFormatter();
      formatter.Serialize(stream, this);
    }

    /// <summary>
    /// Access the specified tree.
    /// </summary>
    /// <param name="index">A zero-based integer index.</param>
    /// <returns>The tree.</returns>
    public Tree<F, S> GetTree(int index)
    {
      return trees_[index];
    }

    /// <summary>
    /// How many trees in the forest?
    /// </summary>
    public int TreeCount
    {
      get { return trees_.Count; }
    }

    /// <summary>
    /// Apply a forest of trees to a set of data points.
    /// </summary>
    /// <param name="data">The data points.</param>
    public int[][] Apply(IDataPointCollection data, ProgressWriter progress = null)
    {
      if (progress == null)
        progress = new ProgressWriter(Verbosity.Interest, Console.Out);

      int[][] leafNodeIndices = new int[TreeCount][];

      for (int t = 0; t < TreeCount; t++)
      {
        progress.Write(Verbosity.Interest, "\rApplying tree {0}...", t);
        leafNodeIndices[t] = trees_[t].Apply(data);
      }
      progress.WriteLine(Verbosity.Interest, "\rApplied {0} trees.      ", TreeCount);

      return leafNodeIndices;
    }
  }
}