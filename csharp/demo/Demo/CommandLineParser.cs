using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  /// <summary>
  /// An abstract representation of a command line parameter used by the 
  /// CommandLineParser class.
  /// </summary>
  abstract class Parameter
  {
    string name_, description_;
    bool bUsed_;

    /// <summary>
    /// The name of the command line parameter.
    /// </summary>
    public virtual string Name
    {
      get
      {
        return name_;
      }
    }

    /// <summary>
    /// A description of the command line parameter.
    /// </summary>
    public virtual string Description
    {
      get
      {
        return description_;
      }
    }

    /// <summary>
    /// Set during command line parsing to indicate whether this parameter
    /// was supplied with an argument or left with its default value.
    /// </summary>
    public bool Used
    {
      get
      {
        return bUsed_;
      }

      internal set
      {
        bUsed_ = value;
      }
    }

    public Parameter(string name, string description)
    {
      name_ = name;
      description_ = description;
    }

    // NOTE: concrete implementaions must not read beyond the end or args[]
    internal abstract int Parse(string[] args, int position);
  }

  /// <summary>
  /// A command line parameter that is just a simple switch. It can be used
  /// or unused but doesn't take any additional arguments.
  /// </summary>
  class SimpleSwitchParameter : Parameter
  {
    public SimpleSwitchParameter(string description)
      : base("", description)
    {
    }

    internal override int Parse(string[] args, int position)
    {
      return position;
    }
  }

  /// <summary>
  /// A command line parameter that takes with a non-empty string as argument.
  /// </summary>
  class StringParameter : Parameter
  {
    public StringParameter(string name, string description, string defaultValue = "")
      : base(name, description)
    {
      Value = defaultValue;
    }

    public override string Description
    {
      get
      {
        return String.Format(base.Description, Value);
      }
    }

    internal override int Parse(string[] args, int position)
    {
      if (position >= args.Length)
        throw new Exception("Insufficient arguments.");

      Value = args[position];
      return position + 1;
    }

    public string Value;
  }

  /// <summary>
  /// A command line parameter that takes a natural number {1, 2, ...} as
  /// argument.
  /// </summary>
  class NaturalParameter : Parameter
  {
    public NaturalParameter(string name, string description, int defaultValue = 1, int maxValue=-1)
      : base(name, description)
    {
      Value = defaultValue;
      MaxValue = maxValue;
    }

    public override string Description
    {
      get
      {
        return String.Format(base.Description, Value);
      }
    }

    internal override int Parse(string[] args, int position)
    {
      if (position >= args.Length)
        throw new Exception("Insufficient arguments.");

      Value = Convert.ToInt32(args[position]);

      if (Value < 1)
        throw new Exception(String.Format("Failed to interpret '{0}' as a natural number.", args[position]));

      if(MaxValue>1 && Value>MaxValue)
        throw new Exception(String.Format("Values greater than {0} are not allowed.", MaxValue));

      return position + 1;
    }

    public int Value;
    public int MaxValue;
  }

  /// <summary>
  /// A command line argument that takes a floating point number as argument.
  /// </summary>
  class SingleParameter : Parameter
  {
    public SingleParameter(string name, string description, bool notNegative = false, bool notZero = false, float defaultValue = 0.0f)
      : base(name, description)
    {
      if (notNegative && defaultValue < 0.0f)
        throw new Exception("Default value must not be negative.");

      if (notZero && defaultValue == 0.0f)
        throw new Exception("Default value must not be zero.");

      NotNegative = notNegative;
      NotZero = notZero;
      Value = defaultValue;
    }

    public override string Description
    {
      get
      {
        return String.Format(base.Description, Value);
      }
    }

    internal override int Parse(string[] args, int position)
    {
      try
      {
        Value = Convert.ToSingle(args[position]);
      }
      catch (Exception)
      {
        throw new Exception("Failed to interpret value as a single precision float.");
      }

      if (NotNegative && Value < 0.0f)
        throw new Exception("Value must not be negative.");

      if (NotZero && Value == 0.0f)
        throw new Exception("Value must not be zero.");

      return position + 1;
    }

    bool NotNegative, NotZero;
    public float Value;
  }

  /// <summary>
  /// A command line argument that takes a member of a set of acceptable
  /// strings as argument.
  /// </summary>
  class EnumParameter : Parameter
  {
    HashSet<string> acceptable_;
    string[] descriptions_;
    public EnumParameter(string name, string description, string[] acceptable, string[] descriptions, string defaultValue = null)
      : base(name, description)
    {
      acceptable_ = new HashSet<string>(acceptable.ToList().ConvertAll(x => x.ToLower()));
      if (defaultValue != null && !acceptable_.Contains(defaultValue.ToLower()))
        throw new ArgumentException("Default value must be one of the specified acceptable values.");
      if (descriptions.Length != acceptable.Length)
        throw new ArgumentException("The number of description strings must be the same as the number acceptable values.");
      descriptions_ = descriptions;
      Value = defaultValue;
    }

    public override string Description
    {
      get
      {
        StringBuilder b = new StringBuilder();

        // If a default value has been provided, substite it for "{0}"
        // in the description string - otherwise just print the description
        // string.
        if (!String.IsNullOrEmpty(Value))
          b.AppendFormat(base.Description + "\n", Value.ToLower());
        else
          b.Append(base.Description + "\n");

        // Print list of acceptable values (slightly hacky because most
        // other layout is done in CommandLineParser::PrintHelp()).
        for (int i = 0; i < acceptable_.Count; i++)
        {
          b.AppendFormat("                 {0} {1}", acceptable_.ElementAt(i).ToLower().PadRight(15), descriptions_[i]);
          if (i != acceptable_.Count - 1)
            b.Append("\n");
        }

        return b.ToString();
      }
    }

    internal override int Parse(string[] args, int position)
    {
      if (position >= args.Length)
        throw new Exception("Insufficient arguments.");

      if (acceptable_.Contains(args[position].ToLower()) == false)
        throw new Exception("Invalid input value.");

      Value = args[position].ToLower();

      return position + 1;
    }

    public string Value;
  }

  /// <summary>
  /// A simple parser intended to facilitate type-safe extraction of
  /// arguments from command lines. Command lines are assumed to contain some
  /// required arguments (in a predefined order) and some optional switches
  /// (in any order). Switches may have parameters, supplied via
  /// </summary>
  class CommandLineParser
  {
    Dictionary<string, Parameter> switches_ = new Dictionary<string, Parameter>();

    List<Parameter> arguments_ = new List<Parameter>();

    string command_;

    public string Command
    {
      set
      {
        command_ = value.ToLower();
      }

      get
      {
        return command_;
      }
    }

    /// <summary>
    /// Add a required argument.
    /// </summary>
    /// <param name="argument">The argument to be added.</param>
    public void AddArgument(Parameter argument)
    {
      arguments_.Add(argument);
    }

    /// <summary>
    /// Add a switch.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="parameter"></param>
    public void AddSwitch(string id, Parameter parameter)
    {
      if (parameter == null)
        throw new ArgumentNullException("parameter");
      switches_.Add(id.ToLower(), parameter);
    }

    /// <summary>
    /// Parse a command line.
    /// </summary>
    /// <param name="args"></param>
    /// <param name="position"></param>
    /// <returns></returns>
    public bool Parse(string[] args, int position = 0)
    {
      int argumentIndex = 0;
      for (int i = position; i < args.Length; )
      {
        // Consume command line switches, i.e. arguments beginning with '/' or '-' and 0...* successive arguments
        while (i < args.Length && args[i].Length >= 1 && (args[i][0] == '/' || args[i][0] == '-'))
        {
          if (args[i].Length == 1)
          {
            Console.WriteLine("Invalid switch.");
            return false;
          }

          string s = args[i].Substring(1).ToLower();

          if (switches_.ContainsKey(s))
          {
            switches_[s].Used = true;
            i += 1;

            try
            {
              i = switches_[s].Parse(args, i);
            }
            catch (Exception e)
            {
              Console.WriteLine("Failed to parse argument for switch /{0}. {1}", s, e.Message);
              return false;
            }
          }
          else
          {
            Console.WriteLine("Invalid switch {0}.", args[i]);
            return false;
          }
        }

        // Consume required arguments, i.e. arguments that are not switches.
        if (i < args.Length)
        {
          if (argumentIndex == arguments_.Count)
          {
            Console.WriteLine("Too many command line arguments.");
            return false;
          }

          try
          {
            i = arguments_[argumentIndex].Parse(args, i);
            arguments_[argumentIndex].Used = true;

            argumentIndex++;
          }
          catch (Exception e)
          {
            Console.WriteLine(e.Message);
            return false;
          }
        }
      }

      // Check that all required args were present
      foreach (Parameter a in arguments_)
      {
        if (a.Used == false)
        {
          Console.WriteLine("Too few command line arguments.");
          return false;
        }
      }

      return true;
    }

    /// <summary>
    /// Pretty print friendly guidance on using the command line.
    /// </summary>
    public void PrintHelp()
    {
      Console.Write(command_ + " ");
      foreach (Parameter argument in arguments_)
        Console.Write(argument.Name.ToUpper() + " ");
      foreach (string option in switches_.Keys)
        Console.Write("[/{0} {1}] ", option.ToLower(), switches_[option].Name.ToLower());
      Console.WriteLine("");
      Console.WriteLine("");
      foreach (Parameter argument in arguments_)
        Console.WriteLine("  {0} {1}", argument.Name.ToUpper().PadRight(10), argument.Description);
      foreach (string s in switches_.Keys)
      {
        string name = "/" + s.ToLower();

        // Add named parameter if one exists
        if (!string.IsNullOrEmpty(switches_[s].Name))
          name += " " + switches_[s].Name.ToUpper();

        Console.WriteLine("  {0} {1}", name.PadRight(10), switches_[s].Description);
      }
      Console.WriteLine("");
    }
  }
}