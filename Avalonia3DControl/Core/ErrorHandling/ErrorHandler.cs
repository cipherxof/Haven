using System;
using System.Diagnostics;

namespace Avalonia3DControl.Core.ErrorHandling
{
    /// <summary>
    /// 统一的错误处理器，提供一致的异常处理策略
    /// </summary>
    public static class ErrorHandler
    {
        /// <summary>
        /// 处理渲染相关的异常
        /// </summary>
        /// <param name="ex">异常对象</param>
        /// <param name="context">异常发生的上下文</param>
        /// <param name="shouldThrow">是否重新抛出异常</param>
        public static void HandleRenderingException(Exception ex, string context, bool shouldThrow = false)
        {
            var message = $"渲染异常 [{context}]: {ex.Message}";
            Debug.WriteLine(message);
            
            if (shouldThrow)
            {
                throw new RenderingException(message, ex);
            }
        }
        
        /// <summary>
        /// 处理初始化相关的异常
        /// </summary>
        /// <param name="ex">异常对象</param>
        /// <param name="context">异常发生的上下文</param>
        public static void HandleInitializationException(Exception ex, string context)
        {
            var message = $"初始化异常 [{context}]: {ex.Message}";
            Debug.WriteLine(message);
            throw new InitializationException(message, ex);
        }
        
        /// <summary>
        /// 处理资源相关的异常
        /// </summary>
        /// <param name="ex">异常对象</param>
        /// <param name="context">异常发生的上下文</param>
        /// <param name="shouldThrow">是否重新抛出异常</param>
        public static void HandleResourceException(Exception ex, string context, bool shouldThrow = true)
        {
            var message = $"资源异常 [{context}]: {ex.Message}";
            Debug.WriteLine(message);
            
            if (shouldThrow)
            {
                throw new ResourceException(message, ex);
            }
        }
        
        /// <summary>
        /// 安全执行操作，捕获并记录异常但不抛出
        /// </summary>
        /// <param name="action">要执行的操作</param>
        /// <param name="context">操作上下文</param>
        /// <returns>操作是否成功执行</returns>
        public static bool SafeExecute(Action action, string context)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"安全执行失败 [{context}]: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 安全执行操作并返回结果
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="func">要执行的函数</param>
        /// <param name="context">操作上下文</param>
        /// <param name="defaultValue">失败时的默认值</param>
        /// <returns>执行结果或默认值</returns>
        public static T SafeExecute<T>(Func<T> func, string context, T defaultValue = default(T)!)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"安全执行失败 [{context}]: {ex.Message}");
                return defaultValue;
            }
        }
    }
    
    /// <summary>
    /// 渲染异常
    /// </summary>
    public class RenderingException : Exception
    {
        public RenderingException(string message) : base(message) { }
        public RenderingException(string message, Exception innerException) : base(message, innerException) { }
    }
    
    /// <summary>
    /// 初始化异常
    /// </summary>
    public class InitializationException : Exception
    {
        public InitializationException(string message) : base(message) { }
        public InitializationException(string message, Exception innerException) : base(message, innerException) { }
    }
    
    /// <summary>
    /// 资源异常
    /// </summary>
    public class ResourceException : Exception
    {
        public ResourceException(string message) : base(message) { }
        public ResourceException(string message, Exception innerException) : base(message, innerException) { }
    }
}