CC=g++
CFLAGS=-c -Wall -I lib
LDFLAGS=
OUTDIR=bin/linux
SOURCES=\
demo/source/Classification.cpp\
demo/source/DataPointCollection.cpp\
demo/source/Regression.cpp\
demo/source/SemiSupervisedClassification.cpp\
demo/source/DensityEstimation.cpp\
demo/source/FloydWarshall.cpp\
demo/source/Graphics.cpp\
demo/source/CommandLineParser.cpp\
demo/source/dibCodec.cpp\
demo/source/main.cpp\
demo/source/StatisticsAggregators.cpp\
demo/source/CumulativeNormalDistribution.cpp\
demo/source/FeatureResponseFunctions.cpp\
demo/source/PlotCanvas.cpp\
demo/source/Platform.cpp

OBJECTS=$(SOURCES:.cpp=.o)
EXECUTABLE=$(OUTDIR)/sw

all: $(SOURCES) $(EXECUTABLE)	
	cp -R demo/data $(OUTDIR)

$(EXECUTABLE): $(OBJECTS)
	mkdir -p $(OUTDIR)
	$(CC) $(LDFLAGS) $(OBJECTS) -o $@

.cpp.o:
	$(CC) $(CFLAGS) $< -o $@

clean: 
	rm -f sw $(OBJECTS)
	rm -r -f $(OUTDIR)

