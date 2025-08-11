using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapConfiguratorCivilization7.Helper
{
    [AttributeUsage(AttributeTargets.Field)]
    public class CheckboxAttribute : Attribute
    {
        public string Name { get; }
        public CheckboxAttribute(string name)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class SliderIntAttribute : Attribute
    {
        public string Name { get; }
        public int Min { get; }
        public int Max { get; }
        public SliderIntAttribute(string name, int min, int max)
        {
            Name = name;
            Min = min;
            Max = max;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class SliderFloatAttribute : Attribute
    {
        public string Name { get; }
        public float Min { get; }
        public float Max { get; }
        public bool IsLog { get; }
        public SliderFloatAttribute(string name, float min, float max, bool isLog = false)
        {
            Name = name;
            Min = min;
            Max = max;
            IsLog = isLog;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class ShowIfAttribute : Attribute
    {
        public string FieldName { get; }
        public ShowIfAttribute(string fieldName)
        {
            FieldName = fieldName;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class HeaderBeginAttribute : Attribute
    {
        public string HeaderName { get; }
        public bool IsDefaultOpen { get; }
        public HeaderBeginAttribute(string headerName, bool isDefaultOpen)
        {
            HeaderName = headerName;
            IsDefaultOpen = isDefaultOpen;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class DescriptionAttribute : Attribute
    {
        public string Description { get; }
        public DescriptionAttribute(string description)
        {
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class HeaderEndAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public class SeparatorAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public class DebugAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public class DropdownAttribute : Attribute
    {
        public string Name { get; }
        public string[] Options { get; }

        public DropdownAttribute(string name, params string[] options)
        {
            Name = name;
            Options = options;
        }
    }
}
