#pragma once

#include <assert.h>

#include <limits>
#include <algorithm>
#include <vector>

namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood
{
  /// <summary>
  /// An implementation of the Floyd-Warshall algorithm for finding shortest
  /// paths between arbitrary points in a (typically dense) undirected, 
  /// weighted graph.
  /// </summary>
  class FloydWarshall
  {
    float* distances_;
    std::vector<int> next_;

    int n_;

  private:
    float& GetDistance(int i, int j)
    {
      assert(j>=i);
      return distances_[i*n_ - (i*(i+1)/2) + j];
    }

    int& GetNext(int i, int j)
    {
      assert(j>=i);
      return next_[i*n_ - (i*(i+1)/2) + j];
    }

  public:

    /// <summary>
    /// Compute paths.
    /// </summary>
    /// <param name="distances">Upper triangular matrix of pairwise inter-node
    /// distances. Will be updated so as to contain distances associated with
    /// shortest path.</param>
    FloydWarshall(float* distances, int n)
    {
      n_ = n;
      distances_ = distances;
      next_ .resize((n*(n+1))/2);

      for (int i = 0; i < n; i++)
        for (int j = i; j < n; j++)
          GetNext(i, j) = i;

      for (int k = 0; k < n; k++)
        for (int i = 0; i < n - 1; i++)
          for (int j = i + 1; j < n; j++)
          {
            if (GetDistance(std::min(i, k), std::max(i,k)) + GetDistance(std::min(k, j), std::max(k,j)) < GetDistance(i, j))
            {
              GetDistance(i, j) = GetDistance(std::min(i, k), std::max(i, k)) + GetDistance(std::min(k, j), std::max(k, j));
              GetNext(i, j) = GetNext(std::min(k, j), std::max(k, j));
            }
          }
    }

    /// <summary>
    /// Computes the shortest path between the specified nodes.
    /// </summary>
    /// <param name="i">Index of one node.</param>
    /// <param name="j">Index of the other node.</param>
    /// <param name="path">Path returned by reference.</param>
    void BuildPath(int i, int j, std::vector<int>& path)
    {
      if (j < i)
      {
        int temp = i;
        i = j;
        j = temp;
      }
      if (i != j)
      {
        if (GetDistance(i, j)==std::numeric_limits<double>::infinity())
        {
          return;
        }
        else
          BuildPath(i, GetNext(i, j), path);
      }
      path.push_back(j);
    }

    /// <summary>
    /// Returns the length of the shortest path between the specified nodes.
    /// </summary>
    /// <param name="i">The index of one node.</param>
    /// <param name="j">The index of the other node.</param>
    /// <returns>The path length.</returns>
    float GetMinimumDistance(int i, int j)
    {
      return GetDistance(std::min(i, j), std::max(i, j));
    }
  };
} } }
