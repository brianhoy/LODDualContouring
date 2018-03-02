using System.Collections.Generic;
using UnityEngine;

namespace SE.DC {

public class OctreeDrawInfo 
{
	public int index;
	public int corners;
	public Vector3 position;
	public Vector3 lod1Position;
	public Vector3 lod1Normal;
	public Vector3 averageNormal;
	public QEF.QEFSolver qef;

	public OctreeDrawInfo()
	{
		index = -1;
		corners = 0;
	}

};

public class OctreeNode
{
	// Brian's util vars
	public float fsize;
	public bool subChunk = false;
	public Vector3 fmin;
	public Vector3Int cmin;

	public DCC.OctreeNodeType type;
	public Vector3 min;
	public int size;

	public OctreeNode[] children;
	public OctreeDrawInfo drawInfo;

	public static int numMinNodes = 0;

	public UtilFuncs.Sampler sample;
	public float isovalue;

	public OctreeNode()
	{
		type = DCC.OctreeNodeType.Node_Internal;
		min = Vector3.zero;
		size = 0;
		drawInfo = null;
	
		children = new OctreeNode[8];
		for (int i = 0; i < 8; i++)
		{
			children[i] = null;
		}
	}

	public OctreeNode(DCC.OctreeNodeType _type)
	{
		type = _type;
		min = Vector3.zero;
		size = 0;
		drawInfo = null;

		for (int i = 0; i < 8; i++)
		{
			children[i] = null;
		}
	}
}
}

