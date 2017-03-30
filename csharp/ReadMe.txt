Sherwood: A software library for decision forest inference
==========================================================

PLEASE VIEW THIS FILE WTIH WORD WRAP ON!


This directory contains the C# version of the Sherwood software library, which was written by Duncan Robertson (duncan.robertson@redimension.co.uk) to accompany the book "A. Criminisi and J. Shotton, Decision Forests: for Computer Vision and Medical Image Analysis. Springer, 2013".

The Sherwood library comprises:
 * a general purpose, object-oriented software framework for applying decision forests to a wide range of inference problems (in "/csharp/lib"); and
 * example code in the form of a command line demo that shows how this framework can be applied to several of the problems described in Part I or the book (in "/csharp/demo").

We hope that the reader will use Sherwood to gain insight into how decision forests work and how they can be implemented. The accompanying example code shows how the general purpose framework can be applied to a variety of toy problems including (i) supervised classification of 2D data, (ii) 1D-1D regression, (iii) 2D density estimation, and (iv) semi-supervised classification of 2D data. The command line demo can be used with the supplied data to reproduce many of the figures in Part I of the book. It is also needed to complete the exercises at the end of the early chapters.

The object oriented framework that forms the heart of Sherwood could also serve as a useful basis for applying decision forests to new inference tasks. To this end, it has been written so as to be easily adaptable. It can support e.g. different types of training data, different weak learners, and different information gain metrics. Whilst the code has been written mainly with simplicity and ease of use in mind, it is nonetheless sufficiently fast for use in non-trivial real world applications.

For more information on the command line demo, including build instructions for Microsoft Visual Studio, please see "/csharp/demo/ReadMe.txt"

For more information on the object oriented software framework, including some advice on how it can be adapted to new decision forest inference tasks, please see "/csharp/lib/ReadMe.txt"

*** PLEASE NOTE *** that this directory contains the C# verion of Sherwood. A C++version is also available, and you may prefer to use this if it suits your needs better, e.g. because you are more familiar with C++ than with C#. For more information see /cpp/Readme.txt.
