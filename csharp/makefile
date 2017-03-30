SOURCES=\
demo/Demo/CommandLineParser.cs\
demo/Demo/Program.cs\
demo/Demo/Usage\ Examples/CumulativeNormalDistribution.cs\
demo/Demo/Usage\ Examples/DataPointCollection.cs\
demo/Demo/Usage\ Examples/FeatureResponseFunctions.cs\
demo/Demo/Usage\ Examples/StatisticsAggregators.cs\
demo/Demo/Usage\ Examples/PlotCanvas.cs\
demo/Demo/Usage\ Examples/Density\ Estimation/DensityEstimation.cs\
demo/Demo/Usage\ Examples/Regression/Regression.cs\
demo/Demo/Usage\ Examples/Semi-supervised\ Classification/Djikstra.cs\
demo/Demo/Usage\ Examples/Semi-supervised\ Classification/FloydWarshall.cs\
demo/Demo/Usage\ Examples/Semi-supervised\ Classification/SemiSupervisedClassification.cs\
demo/Demo/Usage\ Examples/Supervised\ Classification/Classification.cs

LIB=\
lib/Forest.cs\
lib/Tree.cs\
lib/Node.cs\
lib/Interfaces.cs\
lib/ForestTrainer.cs\
lib/ProgressWriter.cs

OUTDIR=bin/mono

bin/mono/sw.exe: ${SOURCES} ${OUTDIR}/sherwood.dll
	echo Building '${OUTDIR}/sw.exe'...
	mkdir -p bin/mono
	mcs -out:$@ ${SOURCES} -r:System.Drawing -r:${OUTDIR}/sherwood.dll
	cp -R demo/Demo/data ${OUTDIR}

bin/mono/sherwood.dll: ${LIB}
	mkdir -p bin/mono
	mcs -t:library -out:$@ ${LIB}

clean:
	rm -r -f ${OUTDIR}

