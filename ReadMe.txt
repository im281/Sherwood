Sherwood: A software library for decision forest inference
==========================================================

PLEASE VIEW THIS FILE WTIH WORD WRAP ON!


This directory contains the C++ version of the Sherwood software library, which was written by Duncan Robertson (duncan.robertson@redimension.co.uk) to accompany the book "A. Criminisi and J. Shotton. Decision Forests: for Computer Vision and Medical Image Analysis. Springer,  2013."

The Sherwood library comprises:
* a general purpose, object-oriented software framework for applying decision forests to a wide range of inference problems; and
* example code in the form of a command line demo that shows how this framework can be applied to several of the problems described in Part I or the book.

We hope that the reader will use Sherwood to gain insight into how decision forests work and how they can be implemented. The accompanying example code shows how the general purpose framework can be applied to a variety of toy problems including (i) supervised classification of 2D data, (ii) 1D-1D regression, (iii) 2D density estimation, and (iv) semi-supervised classification of 2D data. The command line demo can be used with the supplied data to reproduce many of the figures in Part I of the book. It is also needed to complete the exercises at the end of the early chapters.

The object oriented framework that forms the heart of Sherwood could also serve as a useful basis for applying decision forests to new inference tasks. To this end, it has been written so as to be easily adaptable. It can support e.g. different types of training data, different weak learners, and different information gain metrics. Whilst the code has been written mainly with simplicity and ease of use in mind, it is nonetheless sufficiently fast for use in non-trivial real world applications.

Two versions of the software are available, written using C# (for .NET) and C++ (for platform portability). Please use whichever version meets your needs better, e.g. because you are more familiar with C++ than with C#. For more information on the building these two versions, please see the ReadMe.txt file in the appropriate subdirectory.

For further documents, PowerPoint slides, videos and animations please visit:
http://research.microsoft.com/projects/decisionforests

Release Notes v.0.0.0
---------------------

This implementation of Sherwood has the following known limitations:

* The C# version of the command line demo uses a very inefficient strategy for storing low dimensional data points (a managed array or managed arrays) in the DataPointCollection class. See the code comments in DataPointCollection.cs.

* The various command line modes exposed by the demo are limited to 1D and 2D datasets. This is partly by design, so that IStatisticsAggregator and IFeatureResponse can be implemented using simple value types and trees can therefore be stored using simple linear arrays. However, it would be useful to provide  more flexibility, particularly in the case of classification and density estimation.

* The C# and C++ versions of the object oriented framework use different binary file formats for storing forests - so that forests created with one version of the library cannot be read with the other.

* The C++ version of the command line demo provides no mechanism for the user to override the default output file path used to save results.

* The C++ version of the command line demo does not use anti-aliased graphics to draw output images.

