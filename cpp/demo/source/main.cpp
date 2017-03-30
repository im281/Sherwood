#include <stdio.h>

#include <string>
#include <iostream>
#include <fstream>

#include "Platform.h"

#include "Graphics.h"
#include "dibCodec.h"

#include "Sherwood.h"

#include "CumulativeNormalDistribution.h"

#include "CommandLineParser.h"
#include "DataPointCollection.h"

#include "Classification.h"
#include "DensityEstimation.h"
#include "SemiSupervisedClassification.h"
#include "Regression.h"

using namespace MicrosoftResearch::Cambridge::Sherwood;

void DisplayHelp();

void DisplayTextFiles(const std::string& relativePath);

std::auto_ptr<DataPointCollection> LoadTrainingData(
  const std::string& filename,
  const std::string& alternativePath,
  int dimension,
  DataDescriptor::e descriptor);

// Store (Linux-friendly) relative paths to training data
const std::string CLAS_DATA_PATH = "/data/supervised classification";
const std::string SSCLAS_DATA_PATH = "/data/semi-supervised classification";
const std::string REGRESSION_DATA_PATH = "/data/regression";
const std::string DENSITY_DATA_PATH = "/data/density estimation";

int main(int argc, char* argv[])
{
  if(argc<2 || std::string(argv[1])=="/?" || toLower(argv[1])=="help")
  {
    DisplayHelp();
    return 0;
  }

  std::string mode = toLower(argv[1]); //  argv[0] is name of exe, argv[1] defines command line mode

  // These command line parameters are reused over several command line modes...
  StringParameter trainingDataPath("path", "Path of file containing training data.");
  StringParameter forestOutputPath("forest", "Path of file containing forest.");
  StringParameter forestPath("forest", "Path of file containing forest.");
  StringParameter testDataPath("data", "Path of file containing test data.");
  StringParameter outputPath("output", "Path of file containing output.");
  NaturalParameter T("t", "No. of trees in the forest (default = {0}).", 10);
  NaturalParameter D("d", "Maximum tree levels (default = {0}).", 10, 20);
  NaturalParameter F("f", "No. of candidate feature response functions per split node (default = {0}).", 10);
  NaturalParameter L("l", "No. of candidate thresholds per feature response function (default = {0}).", 1);
  SingleParameter a("a", "The number of 'effective' prior observations (default = {0}).", true, false, 10.0f);
  SingleParameter b("b", "The variance of the effective observations (default = {0}).", true, true, 400.0f);
  SimpleSwitchParameter verboseSwitch("Enables verbose progress indication.");
  SingleParameter plotPaddingX("padx", "Pad plot horizontally (default = {0}).", true, false, 0.1f);
  SingleParameter plotPaddingY("pady", "Pad plot vertically (default = {0}).", true, false, 0.1f);

  EnumParameter split(
    "s",
    "Specify what kind of split function to use (default = {0}).",
    "axis;linear",
    "axis-aligned split;linear split",
    "axis");

  // Behaviour depends on command line mode...
  if (mode == "clas" || mode == "class")
  {
    // Supervised classification
    CommandLineParser parser;
    parser.SetCommand("SW CLAS");

    parser.AddArgument(trainingDataPath);
    parser.AddSwitch("T", T);
    parser.AddSwitch("D", D);
    parser.AddSwitch("F", F);
    parser.AddSwitch("L", L);

    parser.AddSwitch("split", split);

    parser.AddSwitch("PADX", plotPaddingX);
    parser.AddSwitch("PADY",  plotPaddingY);
    parser.AddSwitch("VERBOSE", verboseSwitch);

    if (argc == 2)
    {
      parser.PrintHelp();
      DisplayTextFiles(CLAS_DATA_PATH);
      return 0;
    }

    if (parser.Parse(argc, argv, 2) == false)
      return 0;

    TrainingParameters trainingParameters;
    trainingParameters.MaxDecisionLevels = D.Value-1;
    trainingParameters.NumberOfCandidateFeatures = F.Value;
    trainingParameters.NumberOfCandidateThresholdsPerFeature = L.Value;
    trainingParameters.NumberOfTrees = T.Value;
    trainingParameters.Verbose = verboseSwitch.Used();

    PointF plotDilation(plotPaddingX.Value, plotPaddingY.Value);

    // Load training data for a 2D density estimation problem.
    std::auto_ptr<DataPointCollection> trainingData = std::auto_ptr<DataPointCollection> ( LoadTrainingData(
      trainingDataPath.Value,
      CLAS_DATA_PATH + "/" + trainingDataPath.Value,
      2,
      DataDescriptor::HasClassLabels ) );

    if (trainingData.get()==0)
      return 0; // LoadTrainingData() generates its own progress/error messages

    if (split.Value == "linear")
    {
      LinearFeatureFactory linearFeatureFactory;
      std::auto_ptr<Forest<LinearFeatureResponse2d, HistogramAggregator> > forest = ClassificationDemo<LinearFeatureResponse2d>::Train(
        *trainingData,
        &linearFeatureFactory,
        trainingParameters);

      std::auto_ptr<Bitmap<PixelBgr> > result = std::auto_ptr<Bitmap<PixelBgr> >(
        ClassificationDemo<LinearFeatureResponse2d>::Visualize(*forest, *trainingData, Size(300, 300), plotDilation));

      std::cout << "\nSaving output image to result.dib" << std::endl;
      result->Save("result.dib");
    }
    else if (split.Value == "axis")
    {
      AxisAlignedFeatureResponseFactory axisAlignedFeatureFactory;
      std::auto_ptr<Forest<AxisAlignedFeatureResponse, HistogramAggregator> > forest = ClassificationDemo<AxisAlignedFeatureResponse>::Train (
        *trainingData,
        &axisAlignedFeatureFactory,
        trainingParameters );

      std::auto_ptr<Bitmap <PixelBgr> > result = std::auto_ptr<Bitmap <PixelBgr> >(
        ClassificationDemo<AxisAlignedFeatureResponse>::Visualize(*forest, *trainingData, Size(300, 300), plotDilation));

      std::cout << "\nSaving output image to result.dib" << std::endl;
      result->Save("result.dib");
    }
  }
  else if (mode == "density")
  {
    // Density Estimation
    CommandLineParser parser;

    parser.SetCommand("SW " + toUpper(mode));

    parser.AddArgument(trainingDataPath);
    parser.AddSwitch("T", T);
    parser.AddSwitch("D", D);
    parser.AddSwitch("F", F);
    parser.AddSwitch("L", L);

    parser.AddSwitch("split", split);

    // For density estimation (and semi-supervised learning) we add 
    // a command line option to set the hyperparameters of the prior.
    parser.AddSwitch("a", a);
    parser.AddSwitch("b", b);

    parser.AddSwitch("PADX", plotPaddingX);
    parser.AddSwitch("PADY",  plotPaddingY);
    parser.AddSwitch("VERBOSE", verboseSwitch);

    // We also override default values for command line options
    T.Value = 1;
    D.Value = 3;
    F.Value = 5;
    L.Value = 1;
    a.Value = 0;
    b.Value = 900;

    if (argc == 2)
    {
      parser.PrintHelp();
      DisplayTextFiles(DENSITY_DATA_PATH);
      return 0;
    }

    if (parser.Parse(argc, argv, 2) == false)
      return 0;

    TrainingParameters parameters;
    parameters.MaxDecisionLevels = D.Value-1;
    parameters.NumberOfCandidateFeatures = F.Value;
    parameters.NumberOfCandidateThresholdsPerFeature = L.Value;
    parameters.NumberOfTrees = T.Value;
    parameters.Verbose = verboseSwitch.Used();

    // Load training data for a 2D density estimation problem.
    std::auto_ptr<DataPointCollection> trainingData = std::auto_ptr<DataPointCollection>(LoadTrainingData(
      trainingDataPath.Value,
      DENSITY_DATA_PATH + "/" + trainingDataPath.Value,
      2,
      DataDescriptor::Unadorned ) );

    if (trainingData.get()==0)
      return 0; // LoadTrainingData() generates its own progress/error messages

    std::auto_ptr<Forest<AxisAlignedFeatureResponse, GaussianAggregator2d> > forest = std::auto_ptr<Forest<AxisAlignedFeatureResponse, GaussianAggregator2d> >(
      DensityEstimationExample::Train(*trainingData, parameters, a.Value, b.Value) );

    PointF plotDilation(plotPaddingX.Value, plotPaddingY.Value);

    std::auto_ptr<Bitmap <PixelBgr> > result = std::auto_ptr<Bitmap <PixelBgr> >(DensityEstimationExample::Visualize(*(forest.get()), *trainingData, Size(300,300), plotDilation));

    std::cout << "\nSaving output image to result.dib" << std::endl;
    result->Save("result.dib");
  }
  else if (mode == "ssclas" || mode=="ssclass")
  {
    // Semi-supervised classification
    CommandLineParser parser;

    parser.SetCommand(toUpper(mode));

    parser.AddArgument(trainingDataPath);
    parser.AddSwitch("T", T);
    parser.AddSwitch("D", D);
    parser.AddSwitch("F", F);
    parser.AddSwitch("L", L);

    parser.AddSwitch("split", split);

    EnumParameter plotMode(
      "plot",
      "Determines what to plot",
      "density;labels",
      "plot recovered density estimate;plot class likelihood",
      "labels");
    parser.AddSwitch("plot", plotMode);

    parser.AddSwitch("a", a);
    parser.AddSwitch("b", b);

    parser.AddSwitch("PADX", plotPaddingX);
    parser.AddSwitch("PADY",  plotPaddingY);
    parser.AddSwitch("VERBOSE", verboseSwitch);

    // Override default values for command line options
    T.Value = 10;
    D.Value = 12-1;
    F.Value = 30;
    L.Value = 1;

    if (argc == 2)
    {
      parser.PrintHelp();
      DisplayTextFiles(SSCLAS_DATA_PATH);
      return 0;
    }

    if (parser.Parse(argc, argv, 2) == false)
      return 0;

    // Load training data for a 2D density estimation problem.
    std::auto_ptr<DataPointCollection> trainingData = std::auto_ptr<DataPointCollection>(LoadTrainingData(
      trainingDataPath.Value,
      SSCLAS_DATA_PATH + "/" + trainingDataPath.Value,
      2,
      DataDescriptor::HasClassLabels ) );

    if (trainingData.get()==0)
      return 0; // LoadTrainingData() generates its own progress/error messages

    TrainingParameters parameters;
    parameters.MaxDecisionLevels = D.Value-1;
    parameters.NumberOfCandidateFeatures = F.Value;
    parameters.NumberOfCandidateThresholdsPerFeature = L.Value;
    parameters.NumberOfTrees = T.Value;
    parameters.Verbose = verboseSwitch.Used();

    std::auto_ptr<Forest<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator> > forest
      = SemiSupervisedClassificationExample::Train(*trainingData, parameters, a.Value, b.Value );

    PointF plotPadding(plotPaddingX.Value, plotPaddingY.Value);

    if(plotMode.Value=="labels")
    {
      std::auto_ptr<Bitmap<PixelBgr> > result = SemiSupervisedClassificationExample::VisualizeLabels(*forest, *trainingData, Size(300,300), plotPadding);

      std::cout << "\nSaving output image to result.dib" << std::endl;
      result->Save("result.dib");
    }
    else if(plotMode.Value=="density")
    {
      std::auto_ptr<Bitmap<PixelBgr> > result = SemiSupervisedClassificationExample::VisualizeDensity(*forest, *trainingData, Size(300,300), plotPadding);

      std::cout << "\nSaving output image to result.dib" << std::endl;
      result->Save("result.dib");
    }
  }
  else if (mode == "regression")
  {
    // Regression
    CommandLineParser parser;

    parser.SetCommand("SW " + toUpper(mode));

    parser.AddArgument(trainingDataPath);
    parser.AddSwitch("T", T);
    parser.AddSwitch("D", D);
    parser.AddSwitch("F", F);
    parser.AddSwitch("L", L);

    parser.AddSwitch("PADX", plotPaddingX);
    parser.AddSwitch("PADY",  plotPaddingY);
    parser.AddSwitch("VERBOSE", verboseSwitch);

    // Override defaults
    T.Value = 10;
    D.Value = 2;
    a.Value = 0;
    b.Value = 900;

    if (argc == 2)
    {
      parser.PrintHelp();
      DisplayTextFiles(REGRESSION_DATA_PATH);
      return 0;
    }

    if (parser.Parse(argc, argv, 2) == false)
      return 0;

    TrainingParameters parameters;
    parameters.MaxDecisionLevels = D.Value-1;
    parameters.NumberOfCandidateFeatures = F.Value;
    parameters.NumberOfCandidateThresholdsPerFeature = L.Value;
    parameters.NumberOfTrees = T.Value;
    parameters.Verbose = verboseSwitch.Used();

    // Load training data for a 2D density estimation problem.
    std::auto_ptr<DataPointCollection> trainingData = std::auto_ptr<DataPointCollection>(LoadTrainingData(
      trainingDataPath.Value,
      REGRESSION_DATA_PATH + "/" + trainingDataPath.Value,
      1,
      DataDescriptor::HasTargetValues ) );

    if (trainingData.get()==0)
      return 0; // LoadTrainingData() generates its own progress/error messages

    std::auto_ptr<Forest<AxisAlignedFeatureResponse, LinearFitAggregator1d> > forest = RegressionExample::Train(
      *trainingData.get(), parameters);

    PointF plotDilation(plotPaddingX.Value, plotPaddingY.Value);
    std::auto_ptr<Bitmap<PixelBgr> > result = RegressionExample::Visualize(*forest.get(), *trainingData.get(), Size(300,300), plotDilation);

    std::cout << "\nSaving output image to result.dib" << std::endl;
    result->Save("result.dib");
  }
  else
  {
    std::cout << "Unrecognized command line argument, try SW HELP." << std::endl;
    return 0;
  }

  return 0;
}

std::auto_ptr<DataPointCollection> LoadTrainingData(
  const std::string& filename,
  const std::string& alternativePath,
  int dimension,
  DataDescriptor::e descriptor)
{
  std::ifstream r;

  r.open(filename.c_str());

  if(r.fail())
  {
    std::string path;

    try
    {
      path = GetExecutablePath();
    }
    catch(std::runtime_error& e)
    {
      std::cout<< "Failed to determine executable path. " << e.what();
      return std::auto_ptr<DataPointCollection>(0);
    }

    path = path + alternativePath;

    r.open(path.c_str());

    if(r.fail())
    {
      std::cout << "Failed to open either \"" << filename << "\" or \"" << path.c_str() << "\"." << std::endl;
      return std::auto_ptr<DataPointCollection>(0);
    }
  }

  std::auto_ptr<DataPointCollection> trainingData;
  try
  {
    trainingData = DataPointCollection::Load (
      r,
      dimension,
      descriptor );
  }
  catch (std::runtime_error& e)
  {
    std::cout << "Failed to read training data. " << e.what() << std::endl;
    return std::auto_ptr<DataPointCollection>(0);
  }

  if (trainingData->Count() < 1)
  {
    std::cout << "Insufficient training data." << std::endl;
    return std::auto_ptr<DataPointCollection>(0);
  }

  return trainingData;
}

void DisplayTextFiles(const std::string& relativePath)
{
  std::string path;

  try
  {
    path = GetExecutablePath();
  }
  catch(std::runtime_error& e)
  {
    std::cout<< "Failed to find demo data files. " << e.what();
    return;
  }

  path = path + relativePath;

  std::vector<std::string> filenames;

  try
  {
    GetDirectoryListing(path, filenames, ".txt");
  }
  catch(std::runtime_error& e)
  {
    std::cout<< "Failed to list demo data files. " << e.what();
    return;
  }

  if (filenames.size() > 0)
  {
    std::cout << "The following demo data files can be specified as if they were on your current path:-" << std::endl;

    for(std::vector<std::string>::size_type i=0; i<filenames.size(); i++)
      std::cout << "  " << filenames[i].c_str() << std::endl;
  }
}

void DisplayHelp()
{
  // Create a dummy command line parser so we can display command line
  // help in the usual format.
  EnumParameter mode(
    "mode",
    "Select mode of operation.",
    "clas;density;regression;ssclas",
    "Supervised 2D classfication;2D density estimation;1D to 1D regression;Semi-supervised 2D classification");

  StringParameter args("args...", "Other mode-specific arguments");

  CommandLineParser parser;
  parser.SetCommand("SW");
  parser.AddArgument(mode);
  parser.AddArgument(args);

  std::cout << "Sherwood decision forest library demos." << std::endl << std::endl;
  parser.PrintHelp();

  std::cout << "To get more help on a particular mode of operation, omit the arguments, e.g.\nsw density" << std::endl;
}
