#pragma once

#include <string.h>

#include <stdexcept>
#include <string>
#include <vector>
#include <set>
#include <map>
#include <iostream>
#include <algorithm>
#include <cctype>
#include <sstream>

namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood
{
  std::string toLower(const std::string& i);

  std::string toUpper(const std::string& i);

  std::string padRight(const std::string& s,  std::string::size_type n);

  int convertToInt(const std::string& s);

  float convertToSingle(const std::string& s);

  /// <summary>
  /// An abstract representation of a command line parameter used by the 
  /// CommandLineParser class.
  /// </summary>
  class Parameter
  {
    std::string name_, description_;
    bool bUsed_;

  public:
    typedef std::vector<std::string>::size_type ArgumentIndex;

    virtual ~Parameter()
    {
    }

    /// <summary>
    /// The name of the command line parameter.
    /// </summary>
    virtual std::string Name()
    {
      return name_;
    }

    /// <summary>
    /// A description of the command line parameter.
    /// </summary>
    virtual std::string Description()
    {
      return description_;
    }

    /// <summary>
    /// Set during command line parsing to indicate whether this parameter
    /// was supplied with an argument or left with its default value.
    /// </summary>
    bool Used()
    {
      return bUsed_;
    }

    void SetUsed(bool value)
    {
      bUsed_ = value;
    }

    Parameter(const std::string& name, const std::string& description)
    {
      name_ = name;
      description_ = description;
      bUsed_ = false;
    }

    virtual int Parse(const std::vector<std::string>& args, ArgumentIndex position)=0;
  };

  /// <summary>
  /// A command line parameter that is just a simple switch. It can be used
  /// or unused but doesn't take any additional arguments.
  /// </summary>
  class SimpleSwitchParameter : public Parameter
  {
  public:
    SimpleSwitchParameter(const std::string& description)
      : Parameter("", description)
    {
    }

    virtual int Parse(const std::vector<std::string>& args, ArgumentIndex position)
    {
      return position;
    }
  };

  /// <summary>
  /// A command line parameter that takes with a non-empty std::string as argument.
  /// </summary>
  class StringParameter : public Parameter
  {
  public:
    StringParameter(const std::string& name, const std::string& description, const std::string& defaultValue="")
      : Parameter(name, description)
    {
      Value = defaultValue;
    }

    virtual std::string Description()
    {
      const std::string token = "{0}";

      std::string s = Parameter::Description();
      std::string::size_type p = s.find(token);
      if(p!=std::string::npos)
      {
        std::stringstream ss;
        ss << Value;
        s.replace(p, token.size(), ss.str());
      }

      return s;
    }

    virtual int Parse(const std::vector<std::string>& args, ArgumentIndex position)
    {
      if (position >= args.size())
        throw std::runtime_error("Insufficient arguments.");

      Value = args[position];
      return position + 1;
    }

    std::string Value;
  };

  /// <summary>
  /// A command line parameter that takes a natural number {1, 2, ...} as
  /// argument.
  /// </summary>
  class NaturalParameter : public Parameter
  {
  public:
    NaturalParameter(const std::string& name, const  std::string& description, int defaultValue=1, int maxValue=-1)
      : Parameter(name, description)
    {
      MaxValue = maxValue;
      Value = defaultValue;
    }

    virtual std::string Description()
    {
      const std::string token = "{0}";

      std::string s = Parameter::Description();
      std::string::size_type p = s.find(token);
      if(p!=std::string::npos)
      {
        std::stringstream ss;
        ss << Value;
        s.replace(p, token.size(), ss.str());
      }

      return s;
    }

    virtual int Parse(const std::vector<std::string>& args, ArgumentIndex position)
    {
      if (position >= args.size())
        throw std::runtime_error("Insufficient arguments.");

      Value = convertToInt(args[position]);

      if (Value < 1)
      {
        std::stringstream s;
        s << "Failed to interpret '" << args[position] << "' as a natural number.";
        throw std::runtime_error(s.str().c_str());
      }

      if(MaxValue>1 && Value>MaxValue)
      {
        std::stringstream s;
        s << "Values greater than " << MaxValue << " are not allowed.";
        throw std::runtime_error(s.str().c_str());
      }

      return position + 1;
    }

    int Value;
    int MaxValue;
  };

  /// <summary>
  /// A command line argument that takes a floating point number as argument.
  /// </summary>
  class SingleParameter : public Parameter
  {
  public:
    SingleParameter(const std::string& name, const std::string& description, bool notNegative=false, bool notZero=false, float defaultValue=0.0f) 
      : Parameter(name, description)
    {
      if (notNegative && defaultValue < 0.0f)
        throw std::runtime_error("Default value must not be negative.");

      if (notZero && defaultValue == 0.0f)
        throw std::runtime_error("Default value must not be zero.");

      NotNegative = notNegative;
      NotZero = notZero;
      Value = defaultValue;
    }

    virtual std::string Description()
    {
      const std::string token = "{0}";

      std::string s = Parameter::Description();
      std::string::size_type p = s.find(token);
      if(p!=std::string::npos)
      {
        std::stringstream ss;
        ss << Value;
        s.replace(p, token.size(), ss.str());
      }

      return s;
    }

    virtual int Parse(const std::vector<std::string>& args, ArgumentIndex position)
    {
      try
      {
        Value = convertToSingle(args[position]);
      }
      catch (std::runtime_error)
      {
        throw std::runtime_error("Failed to interpret value as a single precision float.");
      }

      if (NotNegative && Value < 0.0f)
        throw std::runtime_error("Value must not be negative.");

      if (NotZero && Value == 0.0f)
        throw std::runtime_error("Value must not be zero.");

      return position + 1;
    }

    float Value;

  private:
    bool NotNegative, NotZero;
  };

  /// <summary>
  /// A command line argument that takes a member of a set of acceptable
  /// strings as argument.
  /// </summary>
  class EnumParameter: public Parameter
  {
    std::set<std::string> acceptableSet_;
    std::vector<std::string> acceptable_;
    std::vector<std::string> descriptions_;

    void tokenize(
      const std::string& str,
      std::vector<std::string>& tokens,
      const std::string& delimiters)
    {
      tokens.clear();

      std::string::size_type lastPos = str.find_first_not_of(delimiters, 0); // skip initial delimeters

      for(std::string::size_type i=0; i<(lastPos==std::string::npos?str.length()+1:lastPos); i++)
        tokens.push_back("");

      std::string::size_type pos = str.find_first_of(delimiters, lastPos); // first "non-delimiter"   

      while (std::string::npos != pos || std::string::npos != lastPos)
      {
        tokens.push_back(str.substr(lastPos, pos - lastPos)); // found token, add to vector 
        lastPos = str.find_first_not_of(delimiters, pos);     // skip delimiters

        if(pos!=std::string::npos)
          for(std::string::size_type i=pos+1; i<(lastPos==std::string::npos?str.length()+1:lastPos); i++)
            tokens.push_back("");

        pos = str.find_first_of(delimiters, lastPos);         // find next "non-delimiter"
      }
    }

  public:
     EnumParameter(
      const std::string& name,
      const std::string& description,
      const std::string& acceptable,
      const std::string& descriptions,
      const std::string& defaultValue="")
      : Parameter(name, description)
    {
      tokenize(acceptable, acceptable_, ";");
      tokenize(descriptions, descriptions_,";");

      for(std::vector<std::string>::const_iterator a=acceptable_.begin(); a!=acceptable_.end(); a++)
        acceptableSet_.insert(toLower(*a));
      if (defaultValue!="" && acceptableSet_.find(toLower(defaultValue))==acceptableSet_.end())
        throw std::runtime_error("Default value must be one of the specified acceptable values.");
      if (descriptions_.size()!= acceptable_.size())
        throw std::runtime_error("The number of description strings must be the same as the number acceptable values.");

      Value = defaultValue;
    }

    virtual std::string Description()
    {
      std::stringstream b;
      // If a default value has been provided, substite it for "{0}"
      // in the description string - otherwise just print the description
      // string.
      if (Value!="")
      {
        const std::string token = "{0}";

        std::string s = Parameter::Description();
        std::string::size_type p = s.find(token);
        if(p!=std::string::npos)
        {
          std::stringstream ss;
          ss << toLower(Value);
          s.replace(p, token.size(), ss.str());
        }

        b << s;
      }
      else
        b << Parameter::Description();

      // Print list of acceptable values (slightly hacky because most
      // other layout is done in CommandLineParser.PrintHelp()).
      b << std::endl;
      for(std::set<std::string>::size_type i=0; i<acceptable_.size(); i++)
      {
        b << "                 " << padRight(toLower(acceptable_[i]), 15)<< " " << descriptions_[i];
        if(i!=acceptable_.size()-1)
          b << "\n";
      }

      return b.str();
    }

    virtual int Parse(const std::vector<std::string>& args, ArgumentIndex position)
    {
      if (position >= args.size())
        throw std::runtime_error("Insufficient arguments.");

      if (acceptableSet_.find(toLower(args[position]))==acceptableSet_.end())
        throw std::runtime_error("Invalid input value.");

      Value = toLower(args[position]);

      return position + 1;
    }

    std::string Value;
  };

  /// <summary>
  /// A simple parser intended to facilitate type-safe extraction of
  /// arguments from command lines. Command lines are assumed to contain some
  /// required arguments (in a predefined order) and some optional switches
  /// (in any order). Switches may have parameters, supplied via
  /// </summary>
  class CommandLineParser
  {
    std::map<std::string, Parameter*> switchMap_;
    std::vector<std::pair<std::string, Parameter*> > switches_;

    std::vector<Parameter*> arguments_;

    std::string command_;

  public:
    std::string Command()
    {
      return command_;
    }

    void SetCommand(const std::string& value)
    {
      command_ = toLower(value);
    }

    /// <summary>
    /// Add a required argument.
    /// </summary>
    /// <param name="argument">The argument to be added.</param>
    void AddArgument(Parameter& argument)
    {
      arguments_.push_back(&argument);
    }

    /// <summary>
    /// Add a switch.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="parameter"></param>
    void AddSwitch(const std::string& id, Parameter& parameter)
    {
      switches_.push_back(std::pair<std::string, Parameter*>(toLower(id), &parameter));
      switchMap_.insert(std::pair<std::string, Parameter*>(toLower(id), &parameter));
    }

    /// <summary>
    /// Parse a command line.
    /// </summary>
    /// <param name="args"></param>
    /// <param name="position"></param>
    /// <returns></returns>
    bool Parse(int argc, char* argv[], int position = 0)
    {
      // Make std::string versions of command line arguments for ease of processing
      std::vector<std::string> args(argc);
      for(int i=0; i<argc; i++)
        args[i] = argv[i];

      Parameter::ArgumentIndex argumentIndex = 0;
      for (Parameter::ArgumentIndex i = position; i < args.size();)
      {
        // Consume command line switches, i.e. arguments beginning with '/' or '-' and 0...* successive arguments
        while(i<args.size() && args[i].size()>=1 && (args[i][0]=='/' || args[i][0]=='-'))
        {
          if(args[i].size()==1)
          {
            std::cout << "Invalid switch." << std::endl;
            return false;
          }

          std::string s = toLower(args[i]);
          s = s.substr(1);

          if(switchMap_.find(s)!=switchMap_.end())
          {
            switchMap_[s]->SetUsed(true);
            i += 1;

            try
            {
              i = switchMap_[s]->Parse(args, i);
            }
            catch (std::runtime_error& e)
            {
              std::cout << "Failed to parse argument for switch /" << s << ". " << e.what() << std::endl;
              return false;
            }
          }
          else
          {
            std::cout << "Invalid switch " << argv[i] << "." << std::endl;
            return false;
          }
        }

        // Consume required arguments, i.e. arguments that are not switches.
        if (i < args.size())
        {
          if (argumentIndex == arguments_.size())
          {
            std::cout << "Too many command line arguments." << std::endl;
            return false;
          }

          try
          {
            i = arguments_[argumentIndex]->Parse(args, i);
            arguments_[argumentIndex]->SetUsed(true);

            argumentIndex++;
          }
          catch (std::runtime_error& e)
          {
            std::cout << e.what() << std::endl;
            return false;
          }
        }
      }

      // Check that all required args were present
      for(std::vector<Parameter*>::const_iterator a = arguments_.begin(); a!=arguments_.end(); a++)
      {
        if ((*a)->Used() == false)
        {
          std::cout << "Too few command line arguments." << std::endl;
        }
      }

      return true;
    }

    /// <summary>
    /// Pretty print friendly guidance on using the command line.
    /// </summary>
    void PrintHelp()
    {
      std::cout << command_ << " ";
      for(std::vector<Parameter*>::const_iterator a=arguments_.begin(); a!=arguments_.end(); a++)
        std::cout << toUpper((*a)->Name()) << " ";
      for(std::vector<std::pair<std::string, Parameter*> >::const_iterator o=switches_.begin(); o!=switches_.end(); o++)
        std::cout << "[/" <<  toLower(o->first) << " "<< toUpper(o->second->Name()) << "] ";
      std::cout << std::endl;
      std::cout << std::endl;
      for(std::vector<Parameter*>::const_iterator a=arguments_.begin(); a!=arguments_.end(); a++)
        std::cout << "  " << padRight(toUpper((*a)->Name()),10) << " " << (*a)->Description() << std::endl;
      for(std::vector<std::pair<std::string, Parameter*> >::const_iterator o=switches_.begin(); o!=switches_.end(); o++)
      {
        std::string name = "/" + toLower(o->first);

        // Add named parameter if one exists
        if (switchMap_[(*o).first]->Name()!="")
          name += " " + toUpper(o->second->Name());

        std::cout << "  " << padRight(name,10) << " " << o->second->Description() << std::endl;
      }
      std::cout << std::endl;
    }
  };
} } }
