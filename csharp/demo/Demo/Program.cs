using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using MicrosoftResearch.Cambridge.Sherwood;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  class Program
  {
    // Store (Linux-friendly) relative paths to training data
    static string CLAS_DATA_PATH = @"data/supervised classification";
    static string SSCLAS_DATA_PATH = @"data/semi-supervised classification";
    static string REGRESSION_DATA_PATH = @"data/regression";
    static string DENSITY_DATA_PATH = @"data/density estimation";

    static void Main(string[] args)
    {
      if (args.Length == 0 || args[0] == "/?" || args[0].ToLower() == "help")
      {
        DisplayHelp();
        return;
      }

      // These command line parameters are reused over several command line modes...
      StringParameter trainingDataPath = new StringParameter("path", "Path of file containing training data.");
      NaturalParameter T = new NaturalParameter("t", "No. of trees in the forest (default = {0}).", 10);
      NaturalParameter D = new NaturalParameter("d", "Maximum tree levels (default = {0}).", 10, 20);
      NaturalParameter F = new NaturalParameter("f", "No. of candidate feature responses per decision node (default = {0}).", 10);
      NaturalParameter L = new NaturalParameter("l", "No. of candidate thresholds per feature response (default = {0}).", 1);
      SingleParameter a = new SingleParameter("a", "The number of 'effective' prior observations (default = {0}).", true, false, 10.0f);
      SingleParameter b = new SingleParameter("b", "The variance of the effective observations (default = {0}).", true, true, 400.0f);
      SimpleSwitchParameter verboseSwitch = new SimpleSwitchParameter("Enables verbose progress indication.");
      SingleParameter plotPaddingX = new SingleParameter("padx", "Pad plot horizontally (default = {0}).", true, false, 0.1f);
      SingleParameter plotPaddingY = new SingleParameter("pady", "Pad plot vertically (default = {0}).", true, false, 0.1f);
      EnumParameter split = new EnumParameter(
          "s",
          "Specify what kind of split function to use (default = {0}).",
          new string[] { "axis", "linear" },
          new string[] { "axis-aligned split", "linear split" },
          "axis");

      // Behaviour depends on command line mode...
      string mode = args[0].ToLower(); // first argument defines the command line mode
      if (mode == "clas" || mode == "class")
      {
        #region Supervised classification
        CommandLineParser parser = new CommandLineParser();

        parser.Command = "SW " + mode.ToUpper();

        parser.AddArgument(trainingDataPath);
        parser.AddSwitch("T", T);
        parser.AddSwitch("D", D);
        parser.AddSwitch("F", F);
        parser.AddSwitch("L", L);
        parser.AddSwitch("SPLIT", split);

        parser.AddSwitch("PADX", plotPaddingX);
        parser.AddSwitch("PADY", plotPaddingY);
        parser.AddSwitch("VERBOSE", verboseSwitch);

        // Default values up above should be fine here.

        if (args.Length == 1)
        {
          parser.PrintHelp();
          DisplayTextFiles(CLAS_DATA_PATH);
          return;
        }

        if (parser.Parse(args, 1) == false)
          return;

        TrainingParameters trainingParameters = new TrainingParameters()
        {
          MaxDecisionLevels = D.Value - 1,
          NumberOfCandidateFeatures = F.Value,
          NumberOfCandidateThresholdsPerFeature = L.Value,
          NumberOfTrees = T.Value,
          Verbose = verboseSwitch.Used
        };

        PointF plotDilation = new PointF(plotPaddingX.Value, plotPaddingY.Value);

        DataPointCollection trainingData = LoadTrainingData(
                trainingDataPath.Value,
                CLAS_DATA_PATH,
                2,
                DataDescriptor.HasClassLabels);

        if (split.Value == "linear")
        {
          Forest<LinearFeatureResponse2d, HistogramAggregator> forest = ClassificationExample.Train(
              trainingData,
              new LinearFeatureFactory(),
              trainingParameters);

          using (Bitmap result = ClassificationExample.Visualize(forest, trainingData, new Size(300, 300), plotDilation))
          {
            ShowVisualizationImage(result);
          }
        }
        else if (split.Value == "axis")
        {
          Forest<AxisAlignedFeatureResponse, HistogramAggregator> forest = ClassificationExample.Train(
              trainingData,
              new AxisAlignedFeatureFactory(),
              trainingParameters);

          using (Bitmap result = ClassificationExample.Visualize(forest, trainingData, new Size(300, 300), plotDilation))
          {
            ShowVisualizationImage(result);
          }
        }
        #endregion
      }
      else if (mode == "density")
      {
        #region Density Estimation
        CommandLineParser parser = new CommandLineParser();

        parser.Command = "SW " + mode.ToUpper();

        parser.AddArgument(trainingDataPath);
        parser.AddSwitch("T", T);
        parser.AddSwitch("D", D);
        parser.AddSwitch("F", F);
        parser.AddSwitch("L", L);

        // For density estimation (and semi-supervised learning) we add 
        // a command line option to set the hyperparameters of the prior.
        parser.AddSwitch("a", a);
        parser.AddSwitch("b", b);

        parser.AddSwitch("PADX", plotPaddingX);
        parser.AddSwitch("PADY", plotPaddingY);
        parser.AddSwitch("VERBOSE", verboseSwitch);

        // Override default values for command line options.
        T.Value = 1;
        D.Value = 3;
        F.Value = 5;
        L.Value = 1;
        a.Value = 0;
        b.Value = 900;

        if (args.Length == 1)
        {
          parser.PrintHelp();
          DisplayTextFiles(DENSITY_DATA_PATH);
          return;
        }

        if (parser.Parse(args, 1) == false)
          return;

        TrainingParameters parameters = new TrainingParameters()
        {
          MaxDecisionLevels = D.Value - 1,
          NumberOfCandidateFeatures = F.Value,
          NumberOfCandidateThresholdsPerFeature = L.Value,
          NumberOfTrees = T.Value,
          Verbose = verboseSwitch.Used
        };

        DataPointCollection trainingData = LoadTrainingData(
                trainingDataPath.Value,
                DENSITY_DATA_PATH,
                2,
                DataDescriptor.Unadorned);

        Forest<AxisAlignedFeatureResponse, GaussianAggregator2d> forest = DensityEstimationExample.Train(trainingData, parameters, a.Value, b.Value);

        PointF plotDilation = new PointF(plotPaddingX.Value, plotPaddingY.Value);

        using (Bitmap result = DensityEstimationExample.Visualize(forest, trainingData, new Size(300, 300), plotDilation))
        {
          ShowVisualizationImage(result);
        }
        #endregion
      }
      else if (mode == "ssclas" || mode == "ssclas")
      {
        #region Semi-supervised classification

        CommandLineParser parser = new CommandLineParser();

        parser.Command = "SW " + mode.ToUpper();

        parser.AddArgument(trainingDataPath);
        parser.AddSwitch("T", T);
        parser.AddSwitch("D", D);
        parser.AddSwitch("F", F);
        parser.AddSwitch("L", L);

        parser.AddSwitch("split", split);

        parser.AddSwitch("a", a);
        parser.AddSwitch("b", b);

        EnumParameter plotMode = new EnumParameter(
            "plot",
            "Determines what to plot",
            new string[] { "density", "labels" },
            new string[] { "plot recovered density estimate", "plot class likelihood" },
            "labels");
        parser.AddSwitch("plot", plotMode);

        parser.AddSwitch("PADX", plotPaddingX);
        parser.AddSwitch("PADY", plotPaddingY);

        parser.AddSwitch("VERBOSE", verboseSwitch);

        // Override default values for command line options.
        T.Value = 10;
        D.Value = 12 - 1;
        F.Value = 30;
        L.Value = 1;

        if (args.Length == 1)
        {
          parser.PrintHelp();
          DisplayTextFiles(SSCLAS_DATA_PATH);
          return;
        }

        if (parser.Parse(args, 1) == false)
          return;

        DataPointCollection trainingData = LoadTrainingData(
                trainingDataPath.Value,
                SSCLAS_DATA_PATH,
                2,
                DataDescriptor.HasClassLabels);

        TrainingParameters parameters = new TrainingParameters()
        {
          MaxDecisionLevels = D.Value - 1,
          NumberOfCandidateFeatures = F.Value,
          NumberOfCandidateThresholdsPerFeature = L.Value,
          NumberOfTrees = T.Value,
          Verbose = verboseSwitch.Used
        };

        Forest<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator> forest = SemiSupervisedClassificationExample.Train(
            trainingData, parameters, a.Value, b.Value);

        PointF plotPadding = new PointF(plotPaddingX.Value, plotPaddingY.Value);

        if (plotMode.Value == "labels")
        {
          using (Bitmap result = SemiSupervisedClassificationExample.VisualizeLabels(forest, trainingData, new Size(300, 300), plotPadding))
          {
            ShowVisualizationImage(result);
          }
        }
        else if (plotMode.Value == "density")
        {
          using (Bitmap result = SemiSupervisedClassificationExample.VisualizeDensity(forest, trainingData, new Size(300, 300), plotPadding))
          {
            ShowVisualizationImage(result);
          }
        }
        #endregion
      }
      else if (mode == "regression")
      {
        #region Regression
        CommandLineParser parser = new CommandLineParser();
        parser.Command = "SW " + mode.ToUpper();

        parser.AddArgument(trainingDataPath);
        parser.AddSwitch("T", T);
        parser.AddSwitch("D", D);
        parser.AddSwitch("F", F);
        parser.AddSwitch("L", L);

        parser.AddSwitch("PADX", plotPaddingX);
        parser.AddSwitch("PADY", plotPaddingY);
        parser.AddSwitch("VERBOSE", verboseSwitch);

        // Override default values for command line options
        T.Value = 10;
        D.Value = 2;
        a.Value = 0; // prior turned off by default
        b.Value = 900;

        if (args.Length == 1)
        {
          parser.PrintHelp();
          DisplayTextFiles(REGRESSION_DATA_PATH);
          return;
        }

        if (parser.Parse(args, 1) == false)
          return;

        RegressionExample regressionDemo = new RegressionExample();

        regressionDemo.PlotDilation.X = plotPaddingX.Value;
        regressionDemo.PlotDilation.Y = plotPaddingY.Value;

        regressionDemo.TrainingParameters = new TrainingParameters()
        {
          MaxDecisionLevels = D.Value - 1,
          NumberOfCandidateFeatures = F.Value,
          NumberOfCandidateThresholdsPerFeature = L.Value,
          NumberOfTrees = T.Value,
          Verbose = verboseSwitch.Used
        };

        DataPointCollection trainingData = LoadTrainingData(
            trainingDataPath.Value,
            REGRESSION_DATA_PATH,
            1,
            DataDescriptor.HasTargetValues);

        using (Bitmap result = regressionDemo.Run(trainingData))
        {
          ShowVisualizationImage(result);
        }
        #endregion
      }
      else
      {
        Console.WriteLine("Unrecognized command line argument, try SW HELP.");
        return;
      }
    }

    static void DisplayTextFiles(string relativePath)
    {
      string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
      path = System.IO.Path.Combine(path+"/", relativePath);
      string[] paths;

      try
      {
        paths = System.IO.Directory.GetFiles(path, "*.txt");
      }
      catch (Exception e)
      {
        Console.WriteLine("Failed to obtain directory listing for data directory. " + e.Message);
        return;
      }

      if (paths.Length > 0)
      {
        Console.WriteLine("The following demo data files can be specified as if they were on your current path:-");

        foreach (string i in paths)
          Console.WriteLine("  " + System.IO.Path.GetFileName(i));
      }
    }

    static void DisplayHelp()
    {
      // Create a dummy command line parser so we can display command line
      // help in the usual format.
      EnumParameter mode = new EnumParameter(
          "mode",
          "Select mode of operation.",
          new string[] { "clas", "density", "regression", "ssclas" },
          new string[] { "Supervised 2D classfication", "2D density estimation", "1D to 1D regression", "Semi-supervised 2D classification" },
          null);

      StringParameter args = new StringParameter("args...", "Other mode-specific arguments");

      CommandLineParser parser = new CommandLineParser();
      parser.Command = "SW";
      parser.AddArgument(mode);
      parser.AddArgument(args);

      Console.WriteLine(
@"Sherwood decision forest library demos.
");
      parser.PrintHelp();

      Console.WriteLine(
@"
To get more help on a particular mode of operation, omit the arguments, e.g.
sw density
");
    }

    static void ShowVisualizationImage(Bitmap b)
    {
      string temporaryPath = Path.Combine(System.IO.Path.GetTempPath(), "visualization.png");

      b.Save(temporaryPath);

      System.Diagnostics.Process.Start(temporaryPath);
    }

    static DataPointCollection LoadTrainingData(
        string path,
        string alternativePath,
        int dimension,
        DataDescriptor dataDescriptor)
    {

      System.IO.FileStream stream = null;
      try
      {
        stream = new FileStream(path, FileMode.Open, FileAccess.Read);
      }
      catch (Exception)
      {
        string a = System.IO.Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/",
                alternativePath);
        a = System.IO.Path.Combine(a, path);
        try
        {
          stream = new FileStream(a, FileMode.Open, FileAccess.Read);
        }
        catch (Exception)
        {
          Console.WriteLine("Failed to open training data file at \"{0}\" or \"{1}\".", path, a);
          Environment.Exit(-1);
        }
      }

      DataPointCollection trainingData = null;
      try
      {
        trainingData = DataPointCollection.Load(
            stream,
            dimension,
            dataDescriptor);
      }
      catch (Exception e)
      {
        Console.WriteLine("Failed to read training data. " + e.Message);
        Environment.Exit(-1);
      }

      if (trainingData.Count() < 1)
      {
        Console.WriteLine("Insufficient training data.");
        Environment.Exit(-1);
      }

      return trainingData;
    }
  }
}
