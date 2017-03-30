Sherwood Library: Command Line Demo
===================================

PLEASE VIEW THIS FILE WTIH WORD WRAP ON!


Introduction
------------

This directory contains C++ source code for a command line tool intended to demonstrate how decision forests may be used to solve a variety of toy inference problems. This code forms a part of the Sherwood software library, which was written by Duncan Robertson (duncan.robertson@redimension.co.uk) to accompany the book "A. Criminisi and J. Shotton, Decision Forests: for Computer Vision and Medical Image Analysis. Springer, 2013".

The command line demo can be used to reproduce many of the figures in the book and is needed to complete the excercises at the end of the theory chapters in Part I. It is hoped that readers will use it to gain more insight into how decision forests work and the effect of training parameters on test accuracy.

The source code depends on the reusable object oriented framework for decision forest training and inference that forms the other part of the Sherwood library. For more information about this framework, and advice on how to use it in your application, please see the accompanying Read Me file (\cpp\lib\ReadMe.txt).

*** PLEASE NOTE *** that this directory contains the C++ verion of Sherwood. A C# version is also available, and you may prefer to use this if it suits your needs better, e.g. because you are more familiar with C# than with C++. For more information see \csharp\Readme.txt.

Windows Installation Instructions
---------------------------------

To use the command line demo on platforms other than Microsoft Windows, it will be necessary to build it from source (see below). However, for the Windows platform, the distribution includes precompiled binary versions of the demo.
 
To use the precompiled version, it will be helpful to add the directory containing the precompiled binary executable to your path. The simplest way of doing this is by using the PATH command at the command prompt. For example, assuming you have copied the library to the directory c:\scratch\sherwood, you would type:

  PATH = %PATH%;c:\scratch\sherwood\cpp\bin\Win32.

and to check that the demo tool is working, just type a command, e.g.:

  SW HELP

which gives more information on the available command line modes.

NOTE that, unlike the C# version of the demo (which saves output images to a temp file before attempting to display them using a shell command), the C++ version saves resulting output images to disk file (result.png). It will be necessary to view resulting output images using your preferred image display software.


Usage
-----

The command line tool can be used to demonstrate the application of decision forest to the followning toy inference problems:
 * 2D supervised classification (command: SW CLAS)
 * 1D-1D linear regression (command: SW REGRESSION)
 * 2D density estimation (command: SW DENSITY)
 * 2D semi-supervised classification (command: SW SSCLAS).

For each command line mode, simple datasets are provided in the form of tab-delimited text files. These can be used to complete the excercises and to repoduce some of the figures in Part I of the book. Type the command with no arguments to get help on command line syntax and to see which data files are available.

You can also create your own data files in the same, simple, tab-delimited format. Data files comprise one or more lines with the following syntax:
[label<TAB>]data0<TAB>[data-1<TAB>][data-2<TAB>]...[data-n<TAB>][target]<CR>
Each data point is a vector of numbers [data-1, data-2, ..., data-n] and may optionally be associated with a string label (for classification problems) or target values (for regression problems). For semi-supervised classification tasks, the empty string "" is a reserved label meaning that the label is unknown.

In all modes, the command line demo produces a simple image as output, typically a visualization of the learned probability density function (PDF) that is encapsulated within the trained decision forest. The intention is that users can simply adjust the training parameters and quickly visualize the effect on the learned PDF. For example, you can easily adjust the following parameters:
 * the number of trees in the forest;
 * the maximum number of decision levels in each tree (tree depth);
 * the maximum number of candidate feature response functions evaluated when training each node;
 * the number of candidate response thresholds to explore per candidate feature response functions.

For more information about the capabilities of the command line demo, you may like to consult the command line help (sw help) or work through the excercises at the end of the theory chapters in Part I or the book.


Build Instructions
------------------

You can also build the demo from source for both Windows and Linux. This is necessary if you wish to use the demo on Linux or if you want to use the demo (or the object oriented software framework on which it depends) as the basis of your own decision forest inference solution.

To build the C++ version of the code for Windows using Visual Studio, the steps are as follows:

1. Open the solution file \cpp\demo\Sherwood.sln.

2. Select the desired solution configuration

3. Select Rebuild All from the Build menu

4. Add the build binary to your path as above.


To build the the code for Linux using g++, the steps are as follows:

1. Navigate to the source code directory (\cpp\demo\source\).

2. Type:
     make all

3. Run the executable (from the \\cpp\bin\linux\ directory) by typing:
 .\sw

