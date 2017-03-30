
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  /// <summary>
  /// An implementation of the Floyd-Warshall algorithm for finding shortest
  /// paths between arbitrary points in a (typically dense) undirected, 
  /// weighted graph.
  /// </summary>
  class FloydWarshall
  {
    float[,] distances_;
    int[,] next_;

    /// <summary>
    /// Compute paths.
    /// </summary>
    /// <param name="distances">Upper triangular matrix of pairwise inter-node
    /// distances. Will be updated so as to contain distances associated with
    /// shortest path.</param>
    public FloydWarshall(float[,] distances)
    {
      if (distances.GetLength(0) != distances.GetLength(1))
        throw new Exception("Distance matrix was not square.");

      int n = distances.GetLength(0);

      distances_ = distances;
      next_ = new int[n, n];

      for (int i = 0; i < n; i++)
        for (int j = i; j < n; j++)
          next_[i, j] = i;

      for (int k = 0; k < n; k++)
        for (int i = 0; i < n - 1; i++)
          for (int j = i + 1; j < n; j++)
          {
            if (distances_[Math.Min(i, k), Math.Max(i, k)] + distances_[Math.Min(k, j), Math.Max(k, j)] < distances_[i, j])
            {
              distances_[i, j] = distances_[Math.Min(i, k), Math.Max(i, k)] + distances_[Math.Min(k, j), Math.Max(k, j)];
              next_[i, j] = next_[Math.Min(k, j), Math.Max(k, j)];
            }
          }
    }

    /// <summary>
    /// Computes the shortest path between the specified nodes.
    /// </summary>
    /// <param name="i">Index of one node.</param>
    /// <param name="j">Index of the other node.</param>
    /// <param name="path">Path returned by reference.</param>
    public void BuildPath(int i, int j, ref List<int> path)
    {
      if (j < i)
      {
        int temp = i;
        i = j;
        j = temp;
      }
      if (i != j)
      {
        if (double.IsPositiveInfinity(distances_[i, j]))
        {
          return;
        }
        else
          BuildPath(i, next_[i, j], ref path);
      }
      path.Add(j);
    }

    /// <summary>
    /// Returns the length of the shortest path between the specified nodes.
    /// </summary>
    /// <param name="i">The index of one node.</param>
    /// <param name="j">The index of the other node.</param>
    /// <returns>The path length.</returns>
    public float GetMinimumDistance(int i, int j)
    {
      return distances_[Math.Min(i, j), Math.Max(i, j)];
    }
  }
}