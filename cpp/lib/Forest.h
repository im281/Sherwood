#pragma once

// This file declares the Forest class, which is used to represent forests
// of decisions trees.

#include <memory>
#include <stdexcept>
#include <fstream>
#include <istream>
#include <iostream>
#include <vector>

#include "ProgressStream.h"

#include "Interfaces.h"
#include "Tree.h"

namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood
{
  /// <summary>
  /// A decision forest, i.e. a collection of decision trees.
  /// </summary>
  template<class F, class S>
  class Forest // where F:IFeatureResponse where S:IStatisticsAggregator<S>
  {
    static const char* binaryFileHeader_;

    std::vector< Tree<F,S>* > trees_;

  public:
    typedef typename std::vector< Tree<F,S>* >::size_type TreeIndex;

    ~Forest()
    {
      for(TreeIndex t=0; t<trees_.size(); t++)
        delete trees_[t];
    }

    /// <summary>
    /// Add another tree to the forest.
    /// </summary>
    /// <param name="path">The tree.</param>
    void AddTree(std::auto_ptr<Tree<F,S> > tree)
    {
      tree->CheckValid();

      trees_.push_back(tree.get());
      tree.release();
    }

    /// <summary>
    /// Deserialize a forest from a file.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The forest.</returns>
    static std::auto_ptr<Forest<F, S> > Deserialize(const std::string& path)
    { 
      std::ifstream i(path.c_str(), std::ios_base::binary);

      return Forest<F,S>::Deserialize(i);
    }

    /// <summary>
    /// Deserialize a forest from a binary stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <returns></returns>
    static std::auto_ptr<Forest<F, S> > Deserialize(std::istream& i)
    {
      std::auto_ptr<Forest<F, S> > forest = std::auto_ptr<Forest<F, S> >(new Forest<F,S>());

      std::vector<char> buffer(strlen(binaryFileHeader_)+1);
      i.read(&buffer[0], strlen(binaryFileHeader_));
      buffer[buffer.size()-1] = '\0';

      if(strcmp(&buffer[0], binaryFileHeader_)!=0)
        throw std::runtime_error("Unsupported forest format.");

      int majorVersion = 0, minorVersion = 0;
      i.read((char*)(&majorVersion), sizeof(majorVersion));
      i.read((char*)(&minorVersion), sizeof(minorVersion));

      if(majorVersion==0 && minorVersion==0)
      {
        int treeCount;
        i.read((char*)(&treeCount), sizeof(treeCount));

        for(int t=0; t<treeCount; t++)
        {
          std::auto_ptr<Tree<F,S> > tree = Tree<F, S>::Deserialize(i);
          forest->trees_.push_back(tree.get());
          tree.release();
        }
      }
      else
        throw std::runtime_error("Unsupported file version number.");

      return forest;
    }

    /// <summary>
    /// Serialize the forest to file.
    /// </summary>
    /// <param name="path">The file path.</param>
    void Serialize(const std::string& path)
    {
      std::ofstream o(path.c_str(), std::ios_base::binary);
      Serialize(o);
    }

    /// <summary>
    /// Serialize the forest a binary stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    void Serialize(std::ostream& stream)
    {
      const int majorVersion = 0, minorVersion = 0;

      stream.write(binaryFileHeader_, strlen(binaryFileHeader_));
      stream.write((const char*)(&majorVersion), sizeof(majorVersion));
      stream.write((const char*)(&minorVersion), sizeof(minorVersion));

      // NB. We could allow IFeatureResponse and IStatisticsAggregrator to
      // write type information here for safer deserialization (and
      // friendlier exception descriptions in the event that the user
      // tries to deserialize a tree of the wrong type).

      int treeCount = TreeCount();
      stream.write((const char*)(&treeCount), sizeof(treeCount));

      for(int t=0; t<TreeCount(); t++)
        GetTree((t)).Serialize(stream);

      if(stream.bad())
        throw std::runtime_error("Forest serialization failed.");
    }

    /// <summary>
    /// Access the specified tree.
    /// </summary>
    /// <param name="index">A zero-based integer index.</param>
    /// <returns>The tree.</returns>
    const Tree<F,S>& GetTree(int index) const
    {
      return *trees_[index];
    }

    /// <summary>
    /// Access the specified tree.
    /// </summary>
    /// <param name="index">A zero-based integer index.</param>
    /// <returns>The tree.</returns>
    Tree<F,S>& GetTree(int index)
    {
      return *trees_[index];
    }

    /// <summary>
    /// How many trees in the forest?
    /// </summary>
    int TreeCount() const
    {
      return trees_.size();
    }

    /// <summary>
    /// Apply a forest of trees to a set of data points.
    /// </summary>
    /// <param name="data">The data points.</param>
    void Apply(
      const IDataPointCollection& data,
      std::vector<std::vector<int> >& leafNodeIndices,
      ProgressStream* progress=0 ) const
    {
      ProgressStream defaultProgressStream(std::cout, Interest);
      progress = (progress==0)?&defaultProgressStream:progress;

      leafNodeIndices.resize(TreeCount());

      for (int t = 0; t < TreeCount(); t++)
      {
        leafNodeIndices[t].resize(data.Count());

        (*progress)[Interest] << "\rApplying tree " << t << "...";
        trees_[t]->Apply(data, leafNodeIndices[t]);
      }

      (*progress)[Interest] << "\rApplied " << TreeCount() << " trees.        " << std::endl;
    }
  };

  template<class F, class S>
  const char* Forest<F,S>::binaryFileHeader_ = "MicrosoftResearch.Cambridge.Sherwood.Forest";
} } }
