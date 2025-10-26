using System;
using FluentAvalonia.UI.Controls;
using LMCUI.Controls;

namespace LMCUI.Utils
{
    /// <summary>
    /// 消息队列帮助类，简化消息队列的使用
    /// </summary>
    public static class MessageQueueHelper
    {
        /// <summary>
        /// 显示信息类型的InfoBar消息
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="content">内容</param>
        /// <param name="duration">持续时间（毫秒），默认为5秒</param>
        /// <param name="isClosable">是否可手动关闭</param>
        /// <returns>消息ID</returns>
        public static string ShowInfo(string title, string content, int duration = 5000, bool isClosable = true)
        {
            if (MessageQueueControl.Instance == null)
                throw new InvalidOperationException("MessageQueueControl is not initialized");

            return MessageQueueControl.Instance.AddInfoBar(title, content, InfoBarSeverity.Informational, duration, isClosable);
        }

        /// <summary>
        /// 显示成功类型的InfoBar消息
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="content">内容</param>
        /// <param name="duration">持续时间（毫秒），默认为5秒</param>
        /// <param name="isClosable">是否可手动关闭</param>
        /// <returns>消息ID</returns>
        public static string ShowSuccess(string title, string content, int duration = 5000, bool isClosable = true)
        {
            if (MessageQueueControl.Instance == null)
                throw new InvalidOperationException("MessageQueueControl is not initialized");

            return MessageQueueControl.Instance.AddInfoBar(title, content, InfoBarSeverity.Success, duration, isClosable);
        }

        /// <summary>
        /// 显示警告类型的InfoBar消息
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="content">内容</param>
        /// <param name="duration">持续时间（毫秒），默认为5秒</param>
        /// <param name="isClosable">是否可手动关闭</param>
        /// <returns>消息ID</returns>
        public static string ShowWarning(string title, string content, int duration = 5000, bool isClosable = true)
        {
            if (MessageQueueControl.Instance == null)
                throw new InvalidOperationException("MessageQueueControl is not initialized");

            return MessageQueueControl.Instance.AddInfoBar(title, content, InfoBarSeverity.Warning, duration, isClosable);
        }

        /// <summary>
        /// 显示错误类型的InfoBar消息
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="content">内容</param>
        /// <param name="duration">持续时间（毫秒），默认为5秒</param>
        /// <param name="isClosable">是否可手动关闭</param>
        /// <returns>消息ID</returns>
        public static string ShowError(string title, string content, int duration = 5000, bool isClosable = true)
        {
            if (MessageQueueControl.Instance == null)
                throw new InvalidOperationException("MessageQueueControl is not initialized");

            return MessageQueueControl.Instance.AddInfoBar(title, content, InfoBarSeverity.Error, duration, isClosable);
        }

        /// <summary>
        /// 显示TeachingTip提示
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="content">内容</param>
        /// <param name="duration">持续时间（毫秒），默认为5秒</param>
        /// <param name="placement">放置位置</param>
        /// <returns>消息ID</returns>
        public static string ShowTeachingTip(string title, string content, int duration = 5000, 
                                            TeachingTipPlacementMode placement = TeachingTipPlacementMode.Top)
        {
            if (MessageQueueControl.Instance == null)
                throw new InvalidOperationException("MessageQueueControl is not initialized");

            return MessageQueueControl.Instance.AddTeachingTip(title, content, duration, placement);
        }

        /// <summary>
        /// 移除指定ID的消息
        /// </summary>
        /// <param name="messageId">消息ID</param>
        public static void RemoveMessage(string messageId)
        {
            if (MessageQueueControl.Instance == null)
                throw new InvalidOperationException("MessageQueueControl is not initialized");

            MessageQueueControl.Instance.RemoveMessage(messageId);
        }
    }
}
