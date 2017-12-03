using System.Collections.Generic;
using System.Collections;
using UnityEngine;


// Fast Uniform Dual Contouring attempt
namespace SE.DC
{
	public static class Algorithm2
	{
		public static readonly Vector3[] CHILD_MIN_OFFSETS = {
			new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 0), new Vector3(0, 1, 1),
			new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(1, 1, 0), new Vector3(1, 1, 1)
		};

		static readonly int[,] FarEdges = { { 3, 7 }, { 5, 7 }, { 6, 7 } };

		public static Mesh Run(int resolution)
		{
        	System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

			sw.Start();
			List<Vector3> vertices = new List<Vector3>();
			List<Vector3> normals = new List<Vector3>();

			OctreeDrawInfo[,,] drawInfos = GenVertices(resolution, vertices, normals);
			Mesh m = GenMesh(resolution, drawInfos, vertices, normals);

			sw.Stop();
			Debug.Log("Fast uniform dual contouring time for " + resolution + "^3 mesh: " + sw.ElapsedMilliseconds + "ms");

			return m;
		}

		public static OctreeDrawInfo[,,] GenVertices(int resolution, List<Vector3> vertices, List<Vector3> normals)
		{
			int[,,] vIds = new int[resolution, resolution, resolution];

			OctreeDrawInfo[,,] drawInfos = new OctreeDrawInfo[resolution, resolution, resolution];

			for (int x = 0; x < resolution; x++)
			{
				for (int y = 0; y < resolution; y++)
				{
					for (int z = 0; z < resolution; z++)
					{
						//Vector3 p = new Vector3(x, y, z);
						byte caseCode = 0;
						sbyte[] densities = new sbyte[8];

						for (int i = 0; i < 8; i++)
						{
							Vector3 pos = new Vector3(x, y, z) + CHILD_MIN_OFFSETS[i];
							densities[i] = (sbyte)Mathf.Clamp((UtilFuncs.Sample(pos.x, pos.y, pos.z) * 127f), -127f, 127f); //data[index + inArray[i]];

							if (densities[i] < 0) { caseCode |= (byte)(1 << i); }
						}

						Vector3[] cpositions = new Vector3[4];
						Vector3[] cnormals = new Vector3[4];
						int edgeCount = 0;
						for (int i = 0; i < 12 && edgeCount < 4; i++)
						{
							byte c1 = (byte)DCC.edgevmap[i][0];
							byte c2 = (byte)DCC.edgevmap[i][1];

							Vector3 p1 = new Vector3(x, y, z) + CHILD_MIN_OFFSETS[c1];
							Vector3 p2 = new Vector3(x, y, z) + CHILD_MIN_OFFSETS[c2];

							bool m1 = ((caseCode >> c1) & 1) == 1;
							bool m2 = ((caseCode >> c2) & 1) == 1;


							if (m1 != m2)
							{
								cpositions[edgeCount] = ApproximateZeroCrossingPosition(p1, p2, UtilFuncs.Sample);
								cnormals[edgeCount] = CalculateSurfaceNormal(cpositions[edgeCount], UtilFuncs.Sample);
								edgeCount++;
							}
						}

						if (edgeCount == 0) continue;

						SE.DC.OctreeDrawInfo drawInfo = new OctreeDrawInfo();
						QEF.QEFSolver qef = new QEF.QEFSolver();
						drawInfo.qef = qef;
						for (int i = 0; i < edgeCount; i++)
						{
							qef.Add(cpositions[i], cnormals[i]);
						}
						drawInfo.position = qef.Solve(0.0001f, 4, 0.0001f);
						drawInfo.index = vertices.Count;
						vertices.Add(drawInfo.position);

						Vector3 max = new Vector3(x, y, z) + Vector3.one;
						if (drawInfo.position.x < x || drawInfo.position.x > max.x ||
							drawInfo.position.y < y || drawInfo.position.y > max.y ||
							drawInfo.position.z < z || drawInfo.position.z > max.z)
						{
							drawInfo.position = drawInfo.qef.MassPoint;
						}

						for (int i = 0; i < edgeCount; i++)
						{
							drawInfo.averageNormal += cnormals[i];
						}
						drawInfo.averageNormal = Vector3.Normalize(drawInfo.averageNormal); //CalculateSurfaceNormal(drawInfo.position, samp);
						normals.Add(drawInfo.averageNormal);
						drawInfo.corners = caseCode;
						drawInfos[x, y, z] = drawInfo;
					}
				}
			}

			return drawInfos;
		}

		public static int earlyContinues;
		public static int lateContinues;
		public static int latelateContinues;

		public static Mesh GenMesh(int resolution, OctreeDrawInfo[,,] drawInfos, List<Vector3> vertices, List<Vector3> normals)
		{
			List<int> indices = new List<int>();

			for (int x = 0; x < resolution; x++)
			{
				for (int y = 0; y < resolution; y++)
				{
					for (int z = 0; z < resolution; z++)
					{
						OctreeDrawInfo drawInfo = drawInfos[x, y, z];
						if (drawInfo == null)
						{
							earlyContinues++;
							continue;
						}
						byte caseCode = (byte)drawInfo.corners;
						Vector3 p = new Vector3(x, y, z);

						int v0 = drawInfo.index;
						for (int edgeNum = 0; edgeNum < 3; edgeNum++)
						{
							if(p.x + 1 >= resolution || p.y + 1 >= resolution || p.z + 1 >= resolution) {
								latelateContinues++;
								continue;
							}

							int ei1 = FarEdges[edgeNum, 0];
							int ei2 = FarEdges[edgeNum, 1];

							bool edge1 = (caseCode & (1 << ei1)) == (1 << ei1);
							bool edge2 = (caseCode & (1 << ei2)) == (1 << ei2);

							if (edge1 == edge2)
							{
								lateContinues++;
								continue;
							}

							int v1, v2, v3;

							if (edgeNum == 0)
							{
								v1 = drawInfos[(int)p.x, (int)p.y, (int)p.z + 1].index;
								v2 = drawInfos[(int)p.x, (int)p.y + 1, (int)p.z].index;
								v3 = drawInfos[(int)p.x, (int)p.y + 1, (int)p.z + 1].index;
							}
							else if (edgeNum == 1)
							{
								v1 = drawInfos[(int)p.x, (int)p.y, (int)p.z + 1].index;
								v2 = drawInfos[(int)p.x + 1, (int)p.y, (int)p.z].index;
								v3 = drawInfos[(int)p.x + 1, (int)p.y, (int)p.z + 1].index;
							}
							else
							{
								v1 = drawInfos[(int)p.x, (int)p.y + 1, (int)p.z].index;
								v2 = drawInfos[(int)p.x + 1, (int)p.y, (int)p.z].index;
								v3 = drawInfos[(int)p.x + 1, (int)p.y + 1, (int)p.z].index;
							}

							int[] t1 = { v0, v1, v3 };
							int[] t2 = { v0, v3, v2 };

							if (((caseCode >> ei1) & 1) == 1 != (edgeNum == 1))
							{ // flip
								t1[0] = v1; t1[1] = v0;
								t2[0] = v3; t2[1] = v0;
							}
							for (int i = 0; i < 3; i++)
							{
								indices.Add(t1[i]);
							}
							for(int i = 0; i < 3; i++) {
								indices.Add(t2[i]);
							}
						}
					}

				}

			}

			Debug.Log("Early continues: " + earlyContinues);
			Debug.Log("Late continues: " + lateContinues);
			Debug.Log("Latelate continues: " + latelateContinues);

			Mesh m = new Mesh();
			m.vertices = vertices.ToArray();
			m.triangles = indices.ToArray();
			m.normals = normals.ToArray();
			return m;

		}

        public static Vector3 Lerp(float density1, float density2, float x1, float y1, float z1, float x2, float y2, float z2)
        {
            if (density1 < 0.00001f && density1 > -0.00001f)
            {
                return new Vector3(x1, y1, z1);
            }
            if (density2 < 0.00001f && density2 > -0.00001f)
            {
                return new Vector3(x2, y2, z2);
            }
            /*if(Mathf.Abs(density1 - density2) < 0.00001f) {
                return new Vector3(x2, y2, z2);
            }*/

            float mu = Mathf.Round((density1) / (density1 - density2) * 256) / 256.0f;

            return new Vector3(x1 + mu * (x2 - x1), y1 + mu * (y2 - y1), z1 + mu * (z2 - z1));
        }
        public static Vector3 LerpN(float density1, float density2, float n1x, float n1y, float n1z, float n2x, float n2y, float n2z)
        {
            float mu = Mathf.Round((density1) / (density1 - density2) * 256f) / 256f;

            return new Vector3(n1x / 127f + mu * (n2x / 127f - n1x / 127f), n1y / 127f + mu * (n2y / 127f - n1y / 127f), n1z / 127f + mu * (n2z / 127f - n1z / 127f));
        }



		public static Vector3 ApproximateZeroCrossingPosition(Vector3 p0, Vector3 p1, UtilFuncs.Sampler sample)
		{
			// approximate the zero crossing by finding the min value along the edge
			float minValue = 100000;
			float t = 0;
			float currentT = 0;
			const int steps = 8;
			const float increment = 1f / (float)steps;
			while (currentT <= 1f)
			{
				Vector3 p = p0 + ((p1 - p0) * currentT);
				float density = Mathf.Abs(sample(p.x, p.y, p.z));
				if (density < minValue)
				{
					minValue = density;
					t = currentT;
				}

				currentT += increment;
			}

			return p0 + ((p1 - p0) * t);
		}

		public static Vector3 CalculateSurfaceNormal(Vector3 p, UtilFuncs.Sampler sample)
		{
			const float H = 0.001f;
			float dx = sample(p.x + H, p.y, p.z) - sample(p.x - H, p.y, p.z);
			float dy = sample(p.x, p.y + H, p.z) - sample(p.x, p.y - H, p.z);
			float dz = sample(p.x, p.y, p.z + H) - sample(p.x, p.y, p.z - H);

			return new Vector3(dx, dy, dz).normalized;
		}
	}
}