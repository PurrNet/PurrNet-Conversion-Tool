using System.Collections.Generic;
using PurrNet.ConversionTool;
using UnityEngine;

public class TestCodeMappings : NetworkSystemMappings
{
    public TestCodeMappings()
    {
        SystemName = "Test";
        TypeMappings = new Dictionary<string, string>{
            {"int", "ushort"}
        };
    }
}