using System;
using System.Runtime.Serialization;

namespace StefanOlsen.Events.Redis
{
    public class KnownTypesBinder : SerializationBinder
    {
        private readonly Type[] _knownTypes;

        public KnownTypesBinder(Type[] knownTypes)
        {
            _knownTypes = knownTypes ?? new Type[0];
        }

        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            var knownType = Array.Find(_knownTypes, t => t == serializedType);

            assemblyName = null;
            typeName = knownType?.FullName;
        }

        public override Type BindToType(string assemblyName, string typeName)
        {
            return Array.Find(_knownTypes, t => t.FullName == typeName);
        }
    }
}
