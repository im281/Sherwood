using Microsoft.Spark.CSharp.Core;
using Microsoft.Spark.CSharp.Services;
using System;
using System.Collections.Generic;

namespace SparkCLR
{
    class Program
    {
        private static ILoggerService Logger;
        static void Main(string[] args)
        {
            //spark context
            var sc = GetSparkContext();
            WordCount(sc, args[0]);
        }
        private static SparkContext GetSparkContext()
        {
            LoggerServiceFactory.SetLoggerService(Log4NetLoggerService.Instance); //this is optional - DefaultLoggerService will be used if not set
            Logger = LoggerServiceFactory.GetLogger(typeof(Program));

            var sparkContext = new SparkContext(new SparkConf().SetAppName("Parsec").SetMaster("local[*]"));
            return sparkContext;
        }
        /// <summary>
        /// Word counting. Flat map all lines where each line returns a list of words
        /// all the lists from each line are then combined to one list. Map then creates
        /// key value pairs of {word,1} and reduce by key counts each value in pairs of two
        /// from left to right where each sum is the input to the left pair of the next sum
        /// Note: You can collect at each step to see what data you have
        /// </summary>
        /// <param name="sc"></param>
        /// <param name="direcoryPath"></param>
        static void WordCount(SparkContext sc, string direcoryPath)
        {         
            var lines = sc.TextFile(direcoryPath, 8)
                .FlatMap(s => s.Split(' '))
                .Map(w => new KeyValuePair<string, int>(w.Trim(), 1))
                .ReduceByKey((x, y) => x + y).Collect();      
        }
    }
}
