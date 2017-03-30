using System;
using System.IO;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  /// <summary>
  /// The verbosity associated with a ProgressWriter (or a progress message
  /// written via a ProgressWriter).
  /// </summary>
  public enum Verbosity { Silent = 0, Error, Warning, Interest, Verbose }

  /// <summary>
  /// Encapsulates writing progress messages to a user-supplied TextWriter
  /// with user-defined verbosity.
  /// </summary>
  public class ProgressWriter
  {
    Verbosity verbosity_;
    System.IO.TextWriter textWriter_;

    public ProgressWriter(Verbosity v, TextWriter w)
    {
      verbosity_ = v;
      textWriter_ = w;
    }

    public void Write(Verbosity v, string template, params object[] args)
    {
      if (v <= verbosity_ && textWriter_ != null)
        textWriter_.Write(template, args);
    }

    public void WriteLine(Verbosity v, string template, params object[] args)
    {
      if (v <= verbosity_ && textWriter_ != null)
        Console.WriteLine(template, args);
    }
  }
}
