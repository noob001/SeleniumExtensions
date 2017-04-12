using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using System.Diagnostics;
using OpenQA.Selenium.Chrome;

namespace SeleniumExtensions
{
    [Serializable]
    public enum Browsers
    {
        [Description("Mozilla Firefox")]
        Firefox,

        [Description("Google Chrome")]
        Chrome
    }

    public static class EnumHelper
    {
        public static string GetEnumDescription(Enum value)
        {
            var fieldName = value.ToString();
            var fieldInfo = value.GetType().GetField(fieldName);
            var attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);

            return attributes.Length > 0 ? attributes[0].Description : fieldName;
        }
    }

    public static class Extensions
    {
        public static bool Contains(this string source, string target, StringComparison stringComparison)
        {
            return source.IndexOf(target, stringComparison) >= 0;
        }

        public static bool Contains(this Uri source, Uri target)
        {
            return source.ToString().Contains(target.ToString());
        }

        public static int ToInt(this string source)
        {
            return string.IsNullOrEmpty(source) ? 0 : int.Parse(source);
        }
    }

    public static class RandomHelper
    {
        public static string RandomString
        {
            get { return Path.GetRandomFileName().Replace(".", string.Empty); }
        }
    }

    public class WaitHelper
    {
        private readonly TimeSpan _timeout;
        private readonly TimeSpan _checkInterval;
        private readonly Stopwatch _stopwatch;
        private bool _isSatisfied = true;

        private WaitHelper(TimeSpan timeout) : this(timeout, TimeSpan.FromSeconds(1))
        {
        }

        private WaitHelper(TimeSpan timeout, TimeSpan checkInterval)
        {
            Contract.Requires(timeout >= TimeSpan.Zero);
            Contract.Requires(checkInterval >= TimeSpan.Zero);

            _timeout = timeout;
            _checkInterval = checkInterval;
            _stopwatch = Stopwatch.StartNew();
        }

        public static WaitHelper WithTimeout(TimeSpan timeout, TimeSpan pollingInterval)
        {
            return new WaitHelper(timeout, pollingInterval);
        }

        public static WaitHelper WithTimeout(TimeSpan timeout)
        {
            return new WaitHelper(timeout);
        }

        public WaitHelper WaitFor(Func<bool> condition)
        {
            Contract.Requires(condition != null);

            if (!_isSatisfied)
            {
                return this;
            }

            while (!condition())
            {
                var sleepAmount = Min(_timeout - _stopwatch.Elapsed, _checkInterval);

                if (sleepAmount < TimeSpan.Zero)
                {
                    _isSatisfied = false;
                    break;
                }

                Thread.Sleep(sleepAmount);
            }

            return this;
        }

        public bool IsSatisfied
        {
            get { return _isSatisfied; }
        }

        public void EnsureSatisfied()
        {
            if (!_isSatisfied)
            {
                throw new TimeoutException();
            }
        }

        public void EnsureSatisfied(string message)
        {
            Contract.Requires(message != null);

            if (!_isSatisfied)
            {
                throw new TimeoutException(message);
            }
        }

        public static bool SpinWait(Func<bool> condition, TimeSpan timeout)
        {
            return SpinWait(condition, timeout, TimeSpan.FromSeconds(1));
        }

        public static bool SpinWait(Func<bool> condition, TimeSpan timeout, TimeSpan pollingInterval)
        {
            return WithTimeout(timeout, pollingInterval).WaitFor(condition).IsSatisfied;
        }

        public static bool Try(Action action)
        {
            Exception exception;

            return Try(action, out exception);
        }

        public static bool Try(Action action, out Exception exception)
        {
            Contract.Requires(action != null);

            try
            {
                action();
                exception = null;

                return true;
            }
            catch (Exception e)
            {
                exception = e;

                return false;
            }
        }

        public static Func<bool> MakeTry(Action action)
        {
            return () => Try(action);
        }

        private static T Min<T>(T left, T right) where T : IComparable<T>
        {
            return left.CompareTo(right) < 0 ? left : right;
        }
    }

    public static class DirectoryHelper
    {
        public static void ForceDelete(string path)
        {
            Contract.Requires(!string.IsNullOrEmpty(path));

            if (!Directory.Exists(path))
            {
                return;
            }

            var baseFolder = new DirectoryInfo(path);

            foreach (var item in baseFolder.EnumerateDirectories("*", SearchOption.AllDirectories))
            {
                item.Attributes = ResetAttributes(item.Attributes);
            }

            foreach (var item in baseFolder.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                item.Attributes = ResetAttributes(item.Attributes);
            }

            baseFolder.Delete(true);
        }

        #region Helpers

        private static FileAttributes ResetAttributes(FileAttributes attributes)
        {
            return attributes & ~(FileAttributes.Archive | FileAttributes.ReadOnly | FileAttributes.Hidden);
        }

        #endregion
    }


}

