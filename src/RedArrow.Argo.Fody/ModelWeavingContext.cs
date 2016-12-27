﻿using Mono.Cecil;
using Mono.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil.Cil;
using RedArrow.Argo.Extensions;

namespace RedArrow.Argo
{
    public class ModelWeavingContext
    {
        // Will log an MessageImportance.Normal message to MSBuild. OPTIONAL
        public Action<string> LogDebug { get; }
        // Will log an MessageImportance.High message to MSBuild. OPTIONAL
        public Action<string> LogInfo { get; }
        // Will log an warning message to MSBuild. OPTIONAL
        public Action<string> LogWarning { get; }
        // Will log an warning message to MSBuild at a specific point in the code. OPTIONAL
        public Action<string, SequencePoint> LogWarningPoint { get; }
        // Will log an error message to MSBuild. OPTIONAL
        public Action<string> LogError { get; }
        // Will log an error message to MSBuild at a specific point in the code. OPTIONAL
        public Action<string, SequencePoint> LogErrorPoint { get; }

        public PropertyDefinition IdPropDef { get; private set; }

        public IEnumerable<PropertyDefinition> MappedAttributes { get; }
        public IEnumerable<PropertyDefinition> MappedHasOnes { get; }
        public IEnumerable<PropertyDefinition> MappedHasManys { get; }

        private TypeDefinition ModelTypeDef { get; }
        public TypeReference ModelTypeRef => ModelTypeDef;

        public Collection<FieldDefinition> Fields => ModelTypeDef.Fields;
        public Collection<MethodDefinition> Methods => ModelTypeDef.Methods;
        public Collection<PropertyDefinition> Properties => ModelTypeDef.Properties;

        public FieldDefinition SessionField { get; set; }

        public ModelWeavingContext(
            TypeDefinition modelTypeDef,
            Action<string> logDebug,
            Action<string> logInfo,
            Action<string> logWarning,
            Action<string, SequencePoint> logWarningPoint,
            Action<string> logError,
            Action<string, SequencePoint> logErrorPoint)
        {
            ModelTypeDef = modelTypeDef;

            LogDebug = logDebug;
            LogInfo = logInfo;
            LogWarning = logWarning;
            LogWarningPoint = logWarningPoint;
            LogError = logError;
            LogErrorPoint = logErrorPoint;
            
            GetMappedIdProperty();

            MappedAttributes = GetMappedProperties(Constants.Attributes.Property);
            MappedHasOnes = GetMappedProperties(Constants.Attributes.HasOne);
            MappedHasManys = GetMappedProperties(Constants.Attributes.HasMany);
        }

        private void GetMappedIdProperty()
        {
            IdPropDef = Properties.SingleOrDefault(x => x.CustomAttributes.ContainsAttribute(Constants.Attributes.Id));

            if (IdPropDef == null)
            {
                LogError($"{ModelTypeDef.FullName} does not have an id property mapped");
            }
            else if (IdPropDef.GetMethod?.ReturnType.FullName != "System.Guid")
            {
                LogError($"{ModelTypeDef} id property must have a System.Guid getter");
            }
        }

        private IEnumerable<PropertyDefinition> GetMappedProperties(string attrFullName)
        {
            return ModelTypeDef.Properties
                .Where(x => x.HasCustomAttributes)
                .Where(p => p.CustomAttributes.ContainsAttribute(attrFullName))
                .ToArray();
        }

        public TypeReference ImportReference(TypeReference typeRef)
        {
            return ModelTypeDef.Module.ImportReference(typeRef);
        }

        public MethodReference ImportReference(MethodReference methRef)
        {
            return ModelTypeDef.Module.ImportReference(methRef);
        }

        public MethodReference ImportReference(MethodReference methRef, IGenericParameterProvider context)
        {
            return ModelTypeDef.Module.ImportReference(methRef, context);
        }
    }
}