﻿using Diagnostics.ModelsAndUtils.Attributes;
using Diagnostics.ModelsAndUtils.Models;
using Diagnostics.Scripts.CompilationService;
using Diagnostics.Scripts.CompilationService.Interfaces;
using Diagnostics.Scripts.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Diagnostics.Scripts
{
    public sealed class EntityInvoker : IDisposable
    {
        private EntityMetadata _entityMetaData;
        private ImmutableArray<string> _frameworkReferences;
        private ImmutableArray<string> _frameworkImports;
        private ICompilation _compilation;
        private ImmutableArray<Diagnostic> _diagnostics;
        private MethodInfo _entryPointMethodInfo;
        private Definition _entryPointDefinitionAttribute;
        private IResourceFilter _resourceFilter;
        private SystemFilter _systemFilter;
        
        public bool IsCompilationSuccessful { get; private set; }

        public IEnumerable<string> CompilationOutput { get; private set; }

        public EntityMetadata EntityMetadata => _entityMetaData;

        public Definition EntryPointDefinitionAttribute => _entryPointDefinitionAttribute;

        public IResourceFilter ResourceFilter => _resourceFilter;

        public SystemFilter SystemFilter => _systemFilter;

        public EntityInvoker(EntityMetadata entityMetadata)
        {
            _entityMetaData = entityMetadata;
            _frameworkReferences = ImmutableArray.Create<string>();
            _frameworkImports = ImmutableArray.Create<string>();
            CompilationOutput = Enumerable.Empty<string>();
        }

        public EntityInvoker(EntityMetadata entityMetadata, ImmutableArray<string> frameworkReferences)
        {
            _entityMetaData = entityMetadata;
            _frameworkReferences = frameworkReferences;
            _frameworkImports = ImmutableArray.Create<string>();
            CompilationOutput = Enumerable.Empty<string>();
        }

        public EntityInvoker(EntityMetadata entityMetadata, ImmutableArray<string> frameworkReferences, ImmutableArray<string> frameworkImports)
        {
            _entityMetaData = entityMetadata;
            _frameworkImports = frameworkImports;
            _frameworkReferences = frameworkReferences;
            CompilationOutput = Enumerable.Empty<string>();
        }

        /// <summary>
        /// Initializes the entry point by compiling the script and loading/saving the assembly
        /// </summary>
        /// <param name="assemblyInitType"></param>
        /// <returns></returns>
        public async Task InitializeEntryPointAsync()
        {
            ICompilationService compilationService = CompilationServiceFactory.CreateService(_entityMetaData, GetScriptOptions(_frameworkReferences));
            _compilation = await compilationService.GetCompilationAsync();
            _diagnostics = await _compilation.GetDiagnosticsAsync();

            IsCompilationSuccessful = !_diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
            CompilationOutput = _diagnostics.Select(m => m.ToString());

            if (IsCompilationSuccessful)
            {
                try
                {
                    EntityMethodSignature methodSignature = _compilation.GetEntryPointSignature();
                    Assembly assembly = await _compilation.EmitAssemblyAsync();
                    _entryPointMethodInfo = methodSignature.GetMethod(assembly);

                    InitializeAttributes();
                    Validate();
                }
                catch(Exception ex)
                {
                    if (ex is ScriptCompilationException scriptEx)
                    {
                        IsCompilationSuccessful = false;

                        if (scriptEx.CompilationOutput.Any())
                        {
                            CompilationOutput = CompilationOutput.Concat(scriptEx.CompilationOutput);
                        }

                        return;
                    }

                    throw ex;
                }
            }
        }
        
        /// <summary>
        /// Initializes the entry point from already loaded assembly.
        /// </summary>
        /// <param name="asm">Assembly</param>
        public void InitializeEntryPoint(Assembly asm)
        {
            if (asm == null)
            {
                throw new ArgumentNullException("Assembly");
            }

            // TODO : We might need to create a factory to get compilation object based on type.
            _compilation = new SignalCompilation();

            // If assembly is present, that means compilation was successful.
            IsCompilationSuccessful = true;

            try
            {
                EntityMethodSignature methodSignature = _compilation.GetEntryPointSignature();
                _entryPointMethodInfo = methodSignature.GetMethod(asm);
                InitializeAttributes();
            }
            catch (Exception ex)
            {
                if (ex is ScriptCompilationException scriptEx)
                {
                    IsCompilationSuccessful = false;

                    if (scriptEx.CompilationOutput.Any())
                    {
                        CompilationOutput = CompilationOutput.Concat(scriptEx.CompilationOutput);
                    }

                    return;
                }

                throw ex;
            }
        }

        public async Task<object> Invoke(object[] parameters)
        {
            if (!IsCompilationSuccessful)
            {
                throw new ScriptCompilationException(CompilationOutput);
            }

            int actualParameterCount = _entryPointMethodInfo.GetParameters().Length;
            parameters = parameters.Take(actualParameterCount).ToArray();
            
            object result = _entryPointMethodInfo.Invoke(null, parameters);

            if (result is Task)
            {
                result = await ((Task)result).ContinueWith(t => GetTaskResult(t));
            }

            return result;
        }

        public async Task<string> SaveAssemblyToDiskAsync(string assemblyPath)
        {
            ICompilationService compilationService = CompilationServiceFactory.CreateService(_entityMetaData, GetScriptOptions(_frameworkReferences));
            _compilation = await compilationService.GetCompilationAsync();
            _diagnostics = await _compilation.GetDiagnosticsAsync();

            IsCompilationSuccessful = !_diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
            CompilationOutput = _diagnostics.Select(m => m.ToString());

            if (!IsCompilationSuccessful)
            {
                throw new ScriptCompilationException(CompilationOutput);
            }

            return await _compilation.SaveAssemblyAsync(assemblyPath);
        }

        public async Task<Tuple<string, string>> GetAssemblyBytesAsync()
        {
            ICompilationService compilationService = CompilationServiceFactory.CreateService(_entityMetaData, GetScriptOptions(_frameworkReferences));
            _compilation = await compilationService.GetCompilationAsync();
            _diagnostics = await _compilation.GetDiagnosticsAsync();
            
            IsCompilationSuccessful = !_diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
            CompilationOutput = _diagnostics.Select(m => m.ToString());

            if (!IsCompilationSuccessful)
            {
                throw new ScriptCompilationException(CompilationOutput);
            }

            return await _compilation.GetAssemblyBytesAsync();
        }

        private void InitializeAttributes()
        {
            if (_entryPointMethodInfo == null)
            {
                return;
            }

            _entryPointDefinitionAttribute = _entryPointMethodInfo.GetCustomAttribute<Definition>();
            if (_entryPointDefinitionAttribute != null)
            {
                _entryPointDefinitionAttribute.SupportTopicList = _entryPointMethodInfo.GetCustomAttributes<SupportTopic>();
            }

            _resourceFilter = _entryPointMethodInfo.GetCustomAttribute<ResourceFilterBase>();
            _systemFilter = _entryPointMethodInfo.GetCustomAttribute<SystemFilter>();
        }

        private ScriptOptions GetScriptOptions(ImmutableArray<string> frameworkReferences)
        {
            ScriptOptions scriptOptions = ScriptOptions.Default;

            if (!frameworkReferences.IsDefaultOrEmpty)
            {
                scriptOptions = ScriptOptions.Default
                    .WithReferences(frameworkReferences);
            }

            if (!_frameworkImports.IsDefaultOrEmpty)
            {
                scriptOptions = scriptOptions.WithImports(_frameworkImports);
            }

            return scriptOptions;
        }
        
        private void Validate()
        {
            if(this._entryPointDefinitionAttribute != null)
            {
                string id = this._entryPointDefinitionAttribute.Id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new ScriptCompilationException("Id cannot be empty in Definition attribute");
                }

                if (string.IsNullOrWhiteSpace(this._entryPointDefinitionAttribute.Name))
                {
                    throw new ScriptCompilationException("Name cannot be empty in Definition attribute");
                }

                // Validate empty author
                if (string.IsNullOrWhiteSpace(this._entryPointDefinitionAttribute.Author))
                {
                    throw new ScriptCompilationException("Author not specified in Definition attribute");
                }

                List<char> invalidChars = Path.GetInvalidFileNameChars().ToList();
                invalidChars.ForEach(x =>
                {
                    if (id.Contains(x))
                    {
                        throw new ScriptCompilationException($"Id(in Definition attribute) cannot contain illegal character : {x}");
                    }

                    if (this._entryPointDefinitionAttribute.Author.Contains(x))
                    {
                        throw new ScriptCompilationException($"Author(in Definition attribute) cannot contain illegal character : {x}");
                    }
                });

                // Validate Support Topic Attributes
                if(this._entryPointDefinitionAttribute.SupportTopicList != null)
                {
                    this._entryPointDefinitionAttribute.SupportTopicList.ToList().ForEach(item =>
                    {
                        if (string.IsNullOrWhiteSpace(item.Id))
                        {
                            throw new ScriptCompilationException("Missing Id from Support Topic Attribute");
                        }

                        if (string.IsNullOrWhiteSpace(item.PesId))
                        {
                            throw new ScriptCompilationException("Missing PesId from Support Topic Attribute");
                        }
                    });

                    if (this._systemFilter == null && this._resourceFilter.InternalOnly && this._entryPointDefinitionAttribute.SupportTopicList.Any())
                    {
                        this.CompilationOutput = this.CompilationOutput.Concat(new string[] { "WARNING: Detector is marked internal and SupportTopic is specified. This means the detector will be enabled for Azure Support Center but not for case submission flow, until the isInternal flag is set to false." });
                    }
                }

                if (this._systemFilter != null && this._resourceFilter != null)
                {
                    throw new ScriptCompilationException("Detector is marked with both SystemFilter and ResourceFilter. System Invoker should not include any ResourceFilter attribute.");
                }
            }
        }

        internal static object GetTaskResult(Task task)
        {
            if (task.IsFaulted)
            {
                throw task.Exception;
            }

            Type taskType = task.GetType();

            if (taskType.IsGenericType)
            {
                return taskType.GetProperty("Result").GetValue(task);
            }

            return null;
        }

        public void Dispose()
        {
            _compilation = null;
            _entryPointMethodInfo = null;
        }
    }
}
