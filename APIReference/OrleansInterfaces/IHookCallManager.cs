using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Orleans.Runtime;
using Orleans.Serialization;
using Orleans.CodeGeneration;
using NQutils.Exceptions;
using NQutils.Serialization;

namespace NQ.Grains.Core;


/// <summary>
///  Throw an instance of this exception to abort call and return in a Pre-hook
/// </summary>
[Serializable]
public class BypassCallWithValue : Exception
{
  public static Type GetTypeFromName(string fullyQualifiedTypeName)
  {
      // Try to get the type directly
      Type type = Type.GetType(fullyQualifiedTypeName);

      if (type != null)
          return type;

      // Fallback: search all loaded assemblies (more expensive)
      foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
      {
          type = asm.GetType(fullyQualifiedTypeName, throwOnError: false);
          if (type != null)
              return type;
      }

      throw new TypeLoadException($"Cannot find type: {fullyQualifiedTypeName}");
  }
  public BypassCallWithValue(object returnVal)
  {
    ReturnValue = returnVal;
  }

  [SerializerMethod]
  static public void Serialize(
    object input,
    ISerializationContext context,
    Type expected)
  {
    var tinput = input as BypassCallWithValue;
    var payload = tinput.ReturnValue;
    var ser = BinarySerializer.SerializeObject(payload);
    SerializationManager.SerializeInner(payload.GetType().FullName, context, typeof(string));
    SerializationManager.SerializeInner(ser, context, typeof(byte[]));
  }

  [DeserializerMethod]
  static public object Deserialize(
    Type expected,
    IDeserializationContext context)
  {
    var strtype = (string)SerializationManager.DeserializeInner(typeof(string), context);
    var type = GetTypeFromName(strtype);
    var payload = (byte[])SerializationManager.DeserializeInner(typeof(byte[]), context);
    using var bd = new BinaryDeserializer(payload);
    var deser = bd.Deserialize(type);
    return new BypassCallWithValue(deser);
  }

  public object ReturnValue;
}

[Orleans.CodeGeneration.SerializerAttribute(typeof(BypassCallWithValue))]
internal class UserSerializer
{
  [CopierMethod]
  public static object DeepCopier(
      object original, ICopyContext context)
  {
    var input = (BypassCallWithValue)original;
    var result = new BypassCallWithValue(input.ReturnValue);
    return result;
  }
  [SerializerMethod]
  public static void Serializer(
        object untypedInput, ISerializationContext context, Type expected)
  {
    BypassCallWithValue.Serialize(untypedInput, context, expected);
  }
  [DeserializerMethod]
  public static object Deserializer(
        Type expected, IDeserializationContext context)
  {
    return BypassCallWithValue.Deserialize(expected, context);
  }
}


public enum HookMode
  {
    Replace, //< Replace the original method entirely
    PreCall, //< Call hook before calling the original method
    PostCall, //< Call hook with result value after calling the original method
  }

// Treat this as an opaque handle
public class HookHandle
{
    public string target;
    public HookMode mode;
    public ulong id;
}

public class ModhookDef
{
  public string target;
  public HookMode mode;
  public string destinationMod;
  public string destinationAction;

}

public class HookInterceptor
{
  public MethodInfo Method;
  public object Object;
  public ulong id;
  public string ModTargetName;
  public string ModTargetAction;
}

public class Interceptors
{
    public List<HookInterceptor> pre = new();
    public List<HookInterceptor> post = new();
    public HookInterceptor repl;
}

/** The IHookCallManager is registered as a singleton and can be obtained from
    an IServiceProvider.
 */
public interface IHookCallManager
{
    /* Register an orleans method hook
       @param hookTarget "classname.methodname", classname is usually the interface
         name minus the first 'I'. Example: "PlayerGrain.GetWallet"
       @param mode how to hook:
         PreCall will call your hook with arguments (grainKeyAsString, args...) and
           then proceed to call the original method
         Replace will call your hook with arguments (ctx, args...) and not call
           the original method, ctx being a IIncomingGrainCallContext instance
         PostCall will call your hook with arguments (grainKeyAsTring, result)
           and return what this hook returns to the caller
       @param callInstance target object to call
       @param callMethod method name on 'callInstance' to call, must not be overloaded and return Task<T>(Replace, Post) or Task(Pre)
       @return a handle for unregistration
    */
    HookHandle Register(string hookTarget, HookMode mode, object callInstance, string callMethod);
    Task RegisterGlobal(string hookTarget, HookMode mode, string modName, string modMethod);
    void Unregister(HookHandle handle);

    Interceptors Acquire(string hookTarget);
}