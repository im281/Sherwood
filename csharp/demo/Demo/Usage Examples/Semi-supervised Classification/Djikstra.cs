//using System;
//using System.Collections.Generic;
//using System.Drawing;
//using System.Text;

//namespace MicrosoftResearch.Cambridge.Sherwood.SemiSupervisedClassificationExample
//{
//    interface IGraph
//    {
//        int VertexCount { get; }
//        float Distance(int u, int v);

//        int[] GetNeighbours(int u);

//        int[] GetNodes();
//    }

//    class LeafNodeGraph<FeatureType>: IGraph
//    {
//        int[] leafIndices_;

//        // The irony is we don't care that the tree has axis aligned features, only that it has GaussianStatisticsAggregator
//        public LeafNodeGraph(Tree<FeatureType> tree)
//        {
//            // TODO: Tempting to move into Tree
//            List<int> leafIndices = new List<int>();
//            for (int i = 0; i < tree.NodeCount; i++)
//                if (tree.GetNode(i).IsLeaf)
//                    leafIndices.Add(i);

//            leafIndices_ = leafIndices.ToArray();
//        }

//        public int VertexCount
//        {
//            get { return leafIndices_.Length; }
//        }

//        public float Distance(int u, int v)
//        {
//            throw new NotImplementedException();
//        }

//        //  TODO: We actually want an enumerable collection of neighbours
//        public int[] GetNeighbours(int u)
//        {
//            return leafIndices_;
//        }

//        public int[] GetNodes()
//        {
//            throw new NotImplementedException();
//        }
//    }

//    // Required functionality is to find the nearest labelled leaf node to each leaf node

//    // Fastest way is to find distances from all labelled leaves to all other leaves

//    // Best way is to find distance from each leaf to all labelled nodes
//    // Then propagate closest histogram

//    class DistanceFinder<T> // or label transducer
//    {
//        List<int> leafIndices_ = new List<int>();
//        List<int> labelledLeafNodeIndices_ = new List<int>();
//        List<int> unlabelledLeafNodeIndices_ = new List<int>();

//        Tree<T> tree_;

//        public float EdgeCost(Tree<T> tree, int i, int j)
//        {
//            i = leafIndices_[i];
//            j = leafIndices_[j];

//            SemiSupervisedClassificationStatisticsAggregator aggregator_i
//                = (SemiSupervisedClassificationStatisticsAggregator)(tree.GetNode(i).TrainingDataStatistics);

//            SemiSupervisedClassificationStatisticsAggregator aggregator_j
//                 = (SemiSupervisedClassificationStatisticsAggregator)(tree.GetNode(j).TrainingDataStatistics);

//            GaussianAggregator2d gaussian_i = aggregator_i.GaussianAggregator2d;
//            GaussianAggregator2d gaussian_j = aggregator_j.GaussianAggregator2d;

//            return 100.0f;
//        }

//        public DistanceFinder(Tree<T> tree)
//        {
//            List<int> leafIndices = new List<int>();
//            List<int> labelledLeafIndices = new List<int>();
//            List<int> unlabelledLeafIndices = new List<int>();
//            for (int i = 0; i < tree.NodeCount; i++)
//            {
//                if (tree.GetNode(i).IsLeaf)
//                {
//                    leafIndices.Add(i);

//                    SemiSupervisedClassificationStatisticsAggregator aggregator
//                        = (SemiSupervisedClassificationStatisticsAggregator)(tree.GetNode(i).TrainingDataStatistics); // TODO: Trap dynamic cast error?
//                    if(aggregator.HistogramAggregator==null) // TODO: Is it really null or empty?
//                        unlabelledLeafIndices.Add(i);
//                    else
//                        labelledLeafIndices.Add(i);
//                }
//            }

//            int n = leafIndices.Count;
//            float[,] path = new float[n,n];
//            for(int i=0; i<n; i++)
//                for(int j=0; j<n; j++)
//                    path[i,j] = EdgeCost(tree, i, j);

//            for(int k=0; k<n; n++)
//                for(int i=0; i<n; i++)
//                    for(int j=0; j<n; j++)
//                        path[i,j] = Math.Min(path[i,j], path[i,k] + path [k,j]); /// Fine but what is the path?

//            // For each unlabelled leaf, propagate label from nearest labelled leaf
//            foreach(int i in leafIndices)
//            {
//                SemiSupervisedClassificationStatisticsAggregator aggregator
//                    = (SemiSupervisedClassificationStatisticsAggregator)(tree.GetNode(i).TrainingDataStatistics); // TODO: Trap dynamic cast error?
//                if(aggregator.HistogramAggregator==null)
//                {
//                }
//            }


//            for (int i = 0; i < tree.NodeCount; i++)
//                if (tree.GetNode(i).IsLeaf)
//                {
//                    SemiSupervisedClassificationStatisticsAggregator aggregator
//                        = (SemiSupervisedClassificationStatisticsAggregator)(tree.GetNode(i).TrainingDataStatistics); // TODO: Trap dynamic cast error?
//                    if(aggregator.HistogramAggregator!=null)
//                        labelledLeafNodeIndices_.Add(i);
//                    else 
//                        unlabelledLeafNodeIndices_.Add(i);
//                }

//            // Now do Djikstra for each unlabelled leaf in turn, finding distances to all nodes
//            // Otherwise we could Djikstra for each labelled leaf in turn, finding distances to all other leaves, and keep a record
//            // at each leaf of the closest label - this likely to be faster assuming fewer unlabelled leaves

//            float[] minDistances = new float[unlabelledLeafNodeIndices_.Count];
//            int[] closestLabelledLeafNodeIndices = new int[unlabelledLeafNodeIndices_.Count];
//            for(int i=0; i<minDistances.Length; i++)
//            {
//                minDistances[i] = float.PositiveInfinity;
//                closestLabelledLeafNodeIndices[i] = -1;
//            }

//            // Fill with positive infinity

//            // For each labelled leaf node
//            foreach (int labelledLeafIndex in labelledLeafNodeIndices_)
//            {
//                // Compute distances to all other leaf nodes
//                float[] distances = new float[]; // TODO

//                // Update minimum distances at all unlabelled leaf nodes
//                foreach(int unlabelledLeafIndex in unlabelledLeafNodeIndices_)
//                {
//                    if(distances[unlabelledLeafIndex]<minDistances[unlabelledLeafIndex])
//                    {
//                        minDistances[unlabelledLeafIndex] = distances[unlabelledLeafIndex];
//                        closestLabelledLeafNodeIndices[unlabelledLeafIndex] = labelledLeafIndex;
//                    }
//                }

//            }

//            // Finally propagate  histograms to unlabelled leaf nodes from closest labelled leaft nodes
//            foreach(int unlabelledLeafIndex in unlabelledLeafNodeIndices_)
//            {
//                // TODO: Broken for value types?
//                SemiSupervisedClassificationStatisticsAggregator aggregator = (SemiSupervisedClassificationStatisticsAggregator)( tree.GetNode(unlabelledLeafIndex).TrainingDataStatistics); // TODO: Trap dynamic cast error?

//                SemiSupervisedClassificationStatisticsAggregator closestLabelledLeafAggregator
//                    = (SemiSupervisedClassificationStatisticsAggregator)( tree.GetNode(closestLabelledLeafNodeIndices[unlabelledLeafIndex]).TrainingDataStatistics); // TODO: Trap dynamic cast error?

//                aggregator.HistogramAggregator = (HistogramAggregator)(closestLabelledLeafAggregator.HistogramAggregator.DeepClone());
//            }
//        }
//    }

//    // float[] FindInterLeafDistances(int leafNodeIndex)
//    // {
//    //    compute 
//    //    foreach(leaf node in set of labelled leaf nodes)
//    //    compute distnace from leafNodeIndex to 

//    class Djikstra
//    {
//        public struct NodeData : IComparable
//        {
//            public NodeData(float distance, int previous)
//            {
//                Distance = distance;
//                Previous = previous;
//            }
//            public float Distance;
//            public int Previous;

//            // IComparable implementation
//            public int CompareTo(object obj)
//            {
//                NodeData rhs = (NodeData)(obj);

//                if (this.Distance < rhs.Distance)
//                    return 1;
//                else if (rhs.Distance < this.Distance)
//                    return -1;
//                else return 0;
//            }
//        };

//        public static NodeData[] Find(IGraph graph, int source)
//        {
//            NodeData[] nodeData = new NodeData[graph.VertexCount];

//            for (int v = 0; v < graph.VertexCount; v++)
//                nodeData[v] = new NodeData(float.PositiveInfinity, -1);

//            nodeData[source].Distance = 0.0f;   // Distance from source to source is zero

//            // For sparse graphs, Dijkstra's algorithm could be implemented more efficiently
//            // by storing the graph in the form of adjacency lists and using a priority queue.

//            HashSet<int> remainingNodes = new HashSet<int>(graph.GetNodes());    // the set of all nodes in Graph 

//            while (remainingNodes.Count != 0)                                    // main loop
//            {
//                int u = -1; // the vertex in Q with smallest distance in dist[], start node in first case

//                float mindist = float.PositiveInfinity;
//                foreach (int key in remainingNodes)
//                {
//                    if (nodeData[key].Distance < mindist)
//                    {
//                        mindist = nodeData[key].Distance;
//                        u = key;
//                    }
//                }

//                if (double.IsPositiveInfinity(mindist))
//                    break;                                          // all remaining vertices are inaccessible from source

//                remainingNodes.Remove(u);

//                foreach (int v in graph.GetNeighbours(u))           // for each neighbour v or u                         
//                {
//                    if (!remainingNodes.Contains(v))                             // where v has not yet been removed from Q.
//                        continue;

//                    float alternativeDistance = nodeData[u].Distance + graph.Distance(u, v);
//                    if (alternativeDistance < nodeData[v].Distance)                         // relax (u,v,a)
//                    {
//                        nodeData[v].Distance = alternativeDistance;
//                        nodeData[v].Previous = u;
                        
//                        // If we maintained a priority queue, we would need to reorder v now
//                    }
//                }
//            }
//            return nodeData;
//        }
//    }
//}
