using System;

namespace AutoWrapper.Filters
{
    public class LogCustomPropertyAttribute : Attribute
    {
        public string Name { get; }
        public object Value { get; }

        public LogCustomPropertyAttribute(string name, object value)
        {
            Name = name;
            Value = value;
        }
    }
}