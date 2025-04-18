using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices; 

namespace SkipLoadingScreen
{
    public static class AsyncMethodPatcher
    {      
        private const string TargetAssemblyName = "Game.dll"; 

        private const string TargetTypeName = "Game.SceneFlow.SplashScreenSequence"; 

        private const string TargetMethodName = "Execute"; 
       
        private const double OriginalDoubleValue = 4.0; 
        
        private const double NewDoubleValue = 0; 

        // --- BepInEx Preloader 入口点 ---
        public static IEnumerable<string> TargetDLLs => new[] { TargetAssemblyName };
        private static readonly ManualLogSource Log = Logger.CreateLogSource("AsyncMethodPatch");

        public static void Patch(AssemblyDefinition assembly)
        {
            Log.LogInfo($"开始为程序集打补丁: {assembly.Name.Name}");
            
            var targetType = assembly.MainModule.GetType(TargetTypeName);
            if (targetType == null)
            {
                Log.LogError($"未在程序集 {assembly.Name.Name} 中找到目标类型: {TargetTypeName}");
                return;
            }
            Log.LogInfo($"找到类型: {targetType.FullName}");

            TypeReference iasyncStateMachineTypeRef = assembly.MainModule.ImportReference(typeof(IAsyncStateMachine));
            if (iasyncStateMachineTypeRef == null)
            {
                Log.LogError($"无法导入类型引用。");
                return;
            }

            var stateMachineType = targetType.NestedTypes
                .FirstOrDefault(t => t.Name.StartsWith($"<{TargetMethodName}>d__") 
                                  && t.Interfaces.Any(iface => iface.InterfaceType.FullName == iasyncStateMachineTypeRef.FullName)); 

            if (stateMachineType == null)
            {
                Log.LogError($"在类型 {targetType.FullName} 中未找到与方法 {TargetMethodName} ");               
                return;
            }
            Log.LogInfo($"找到方法");

            var moveNextMethod = stateMachineType.Methods
                .FirstOrDefault(m => m.Name == "MoveNext"
                                  && m.ReturnType.FullName == assembly.MainModule.TypeSystem.Void.FullName 
                                  && !m.HasParameters); 

            if (moveNextMethod == null)
            {
                Log.LogError($"未找到方法。");
                return;
            }
            Log.LogInfo($"找到方法: {moveNextMethod.Name}");
            
            if (!moveNextMethod.HasBody)
            {
                Log.LogWarning($"没有方法体，无法打补丁。");
                return;
            }

         
            var processor = moveNextMethod.Body.GetILProcessor();
            bool patched = false;

            for (int i = 0; i < moveNextMethod.Body.Instructions.Count; i++)
            {
                var instruction = moveNextMethod.Body.Instructions[i];
                             
                if (instruction.OpCode == OpCodes.Ldc_R8 &&
                    instruction.Operand is double operandValue &&
                    operandValue == OriginalDoubleValue)
                {
                    Log.LogInfo($"找到匹配指令。");
                   
                    instruction.Operand = NewDoubleValue;

                    Log.LogInfo($"指令操作数已修改为: {NewDoubleValue}");
                    patched = true;
                }
            }

            if (patched)
            {
                Log.LogInfo($"成功应用了补丁。");
            }
            else
            {
                Log.LogWarning($"补丁未应用。请检查配置是否正确。");
            }
        }
    }
}
