Sherwood: Object oriented framework
===================================

PLEASE VIEW THIS FILE WTIH WORD WRAP ON!


Introduction
------------

This directory contains C++ source code for a reusable object oriented framework designed to be used as the basis of decision forest inference software. This is a part of the Sherwood code library, which was written by Duncan Robertson (duncan.robertson@redimension.co.uk) to accompany the book "A. Criminisi and J. Shotton, Decision Forests: for Computer Vision and Medical Image Analysis. Springer, 2013".

The object oriented framework contained within this directory forms the basis of a command line demo tool that accompanies Sherwood (and is required to complete the exercises in Part I of the book). The demo provides a useful illustration of how the framework can be applied simply to a variety of different decision forest inference tasks, and may be a useful place to get started (see \cpp\demo\ReadMe.txt).

The framework has been written mainly with simplicity and ease of use in mind (rather than performance per se). Nevertheless, it is sufficiently performant for use in many real world applications.

*** PLEASE NOTE *** that this directory contains the C++ verion of Sherwood. A C# version is also available, and you may prefer to use this if it suits your needs better, e.g. because you are more familiar with C# than with C++. For more information see \csharp\Readme.txt.


Architecture
------------

Within the library decision forests are represented by the Forest, Tree, and Node classes. Naturally, a Forest contains one or more Tree instances, and each Tree contains one or more Node instances. A Node can represent either a split node or a leaf node. Both split nodes and leaf nodes have a set of training sample statistics (an IStatisticsAggregator). Additionally, split nodes have an associated weak learner, i.e. the feature response function selected during the forest training procedure (an IFeatureResponseResponse) and an associated decision threshold.

Decision forest training is the responsibility of the ForestTrainer class. So that the training framework can be simply reused across problem domains, training tasks are represented by abstract interfaces that need to be implemented within client code. These interfaces abstract out what is common amongst decision forest training implementations, such as the computation of information gain over candidate partitions of a set of data points, or how to decide when to stop training a branch of the tree. The important interfaces are as follows:

 * IDataPointCollection. This class represents a collection of data points. In principle, a data point could represent anything for which a scalar feature response can be computed, e.g. a vector, an image, or a pixel in an image. Since data points can have various forms, the training framework interacts with collections of data points only via the IDataPointCollection abstraction.

 * IStatisticsAggregator. This class is responsible for aggregating statistics over subsets of the training data points. During training, these statistics are used both (i) to compute information gain over partitions of the data resulting from the application of candidate weak learners, and (ii) to decide when to stop training a particular tree branch. At test time, statistics computed during training are used for inference.  Which statistics should be aggregated depends on the problem at hand. For example, when training a classification tree, we would typically aggregate histograms over the data points' class labels.

 * IFeatureResponseResponse. This class is responsible for computing feature responses on incoming data points, i.e. for mapping data points to scalar values. Combined with a threshold parameter, feature response functions form the basis of the weak learners used to form binary partitions over sets of data points. In Sherwood, feature response function parameters are generated at random by the client code. For illustration, a simple weak learner might partition D-dimensional data points in by splitting them by a plane; in this case the feature response function might be parameterized by the D-dimensinal plane normal.

 * ITrainingContext. This is the main interface by which the general purpose training framework interacts with the training data. At training time, application-specific implementations of ITrainingContext are responsible for random generation of candidate feature response functions (IFeatureResponse instances), for creating IStatisticsAggregator instances, and for the computation (based on statistics computed over training data points at parent and child nodes) of the information gain associated with each candidate weak learner.


Applying the framework
----------------------

To use Sherwood's object oriented decision forest framework within your own project, all that is necessary is to add the directory containing the constituent header files (Sherwood.h, Forest.h, etc.) to your include directory search path. Then add the following line to your C++ file:
  #include "Sherwood.h"
If your compiler supports OpenMP (e.g. Visual Studio 2010, g++ on most modern Linux flavours), you may also like to use the parallel version of the ForestTrainer class, ParallelForestTrainer (#include "ParallelTreeTrainer.h"). This has essentially the same interface as, but may be faster in applications where it is necessary to evaluate many candidate features per node during forest training.

To use the object oriented framework in a particular problem domain, the following steps will be required:

1. Implement the abstract interfaces by which the training framework interacts with the training data. These are: IDataPointCollection, IFeatureResponseResponse, IStatisticsAggregator, and ITrainingContext.

2. Use the ForestTrainer::TrainForest() method to create a new Forest. Alternatively, if you have compiled the ParallelForestTrainer class (which requires OpenMP support) and you wish to parallelize the node training over candidate features, you could call ParallelForestTrainer::TrainForest()).

3. Optionally serialize the trained forest to a binary file for later deserialization and use.

4. Apply the trained forest to test data: tune parameters on a validation set, and then apply the forest to a previously unseen test set.

For an example of how the framework has been adapted to a toy classification problem, please see the classes declared and defined in Classification.h and Classification.cpp in the /cpp/demo/source subdirectory.

For more information about how to adapt the framework to particular problem domain, please see the book.
