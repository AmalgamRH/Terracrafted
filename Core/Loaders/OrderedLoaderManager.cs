using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria.ModLoader;

namespace TerraCraft.Core.Loaders;
public static class OrderedLoaderManager
{
    private static readonly List<IOrderedLoadable> _loadables = new();
    private static bool _autoRegistered = false;

    /// <summary>
    /// 自动发现并注册当前程序集中所有实现了 IOrderedLoadable 的非抽象类
    /// </summary>
    /// <param name="assembly">要扫描的程序集，默认是调用方程序集</param>
    public static void AutoRegister(Assembly assembly = null)
    {
        if (_autoRegistered) return; // 防止重复注册

        assembly ??= Assembly.GetCallingAssembly();
        var loadableTypes = assembly.GetTypes()
            .Where(t => typeof(IOrderedLoadable).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        foreach (var type in loadableTypes)
        {
            try
            {
                var instance = Activator.CreateInstance(type) as IOrderedLoadable;
                if (instance != null)
                    Register(instance);
            }
            catch (Exception ex)
            {
                TerraCraft.Instance.Logger.
                    Warn($"Failed to create instance of {type.FullName}: {ex.Message}");
            }
        }

        _autoRegistered = true;
    }

    /// <summary>
    /// 手动注册一个加载任务
    /// </summary>
    public static void Register(IOrderedLoadable loadable)
    {
        if (loadable == null) throw new ArgumentNullException(nameof(loadable));
        _loadables.Add(loadable);
    }

    /// <summary>
    /// 按优先级升序执行所有 Load
    /// </summary>
    public static void ExecuteAllLoad()
    {
        foreach (var loadable in _loadables.OrderBy(l => l.Priority))
            loadable.Load();
    }

    /// <summary>
    /// 按优先级降序执行所有 Unload（后加载的先卸载）
    /// </summary>
    public static void ExecuteAllUnload()
    {
        foreach (var loadable in _loadables.OrderByDescending(l => l.Priority))
            loadable.Unload();
    }

    /// <summary>
    /// 清空注册列表（通常在模组 Unload 时调用）
    /// </summary>
    public static void Clear()
    {
        _loadables.Clear();
        _autoRegistered = false;
    }
}