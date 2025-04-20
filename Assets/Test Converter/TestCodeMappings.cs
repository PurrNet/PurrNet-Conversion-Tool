using System.Collections.Generic;
using PurrNet.ConversionTool;
using UnityEngine;

public class TestCodeMappings : NetworkSystemMappings
{
    public TestCodeMappings()
    {
        SystemName = "Test";
        PropertyMappings = new Dictionary<string, string>{{"string", "ulong"}, };
    }
}