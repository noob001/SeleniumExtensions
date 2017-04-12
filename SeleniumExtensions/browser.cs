using System;
using System.Collections.Generic;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using System.Diagnostics.Contracts;
using System.IO;
using System.Drawing.Imaging;
using OpenQA.Selenium.Interactions;
using System.Threading;
using System.Linq;

namespace SeleniumExtensions
{
    class Browser
    {

        #region Public methods


        public static Browsers SelectedBrowser
        {
            get { return Browsers.Chrome; }
        }

        public static Uri Url
        {
            get { return new Uri(WebDriver.Url); }
        }

        public static string Title
        {
            get
            {
                return string.Format("{0} - {1}", WebDriver.Title, EnumHelper.GetEnumDescription(SelectedBrowser));
            }
        }

        public static string PageSource
        {
            get { return WebDriver.PageSource; }
        }


        public static void Navigate(Uri url)
        {
            Contract.Requires(url != null);

            WebDriver.Navigate().GoToUrl(url);
        }



        public static void WaitReadyState()
        {
            Contract.Assume(WebDriver != null);
            var ready = new Func<bool>(() => (bool)ExecuteJavaScript("return document.readyState == 'complete'"));
            Contract.Assert(WaitHelper.SpinWait(ready, TimeSpan.FromSeconds(60), TimeSpan.FromMilliseconds(100)));
        }

        public static void WaitAjax()
        {
            Contract.Assume(WebDriver != null);
            var ready = new Func<bool>(() => (bool)ExecuteJavaScript("return (typeof($) === 'undefined') ? true : !$.active;"));
            Contract.Assert(WaitHelper.SpinWait(ready, TimeSpan.FromSeconds(60), TimeSpan.FromMilliseconds(100)));
        }


        public static IEnumerable<IWebElement> FindElements(By selector)
        {
            Contract.Assume(WebDriver != null);

            return WebDriver.FindElements(selector);
        }


        public static Screenshot GetScreenshot()
        {
            WaitReadyState();

            return ((ITakesScreenshot)WebDriver).GetScreenshot();
        }

        public static void SaveScreenshot(string path)
        {
            Contract.Requires(!string.IsNullOrEmpty(path));

            GetScreenshot().SaveAsFile(path, ImageFormat.Jpeg);
        }



        public static void ResizeWindow(int width, int height)
        {
            ExecuteJavaScript(string.Format("window.resizeTo({0}, {1});", width, height));
        }


        public static void NavigateBack()
        {
            WebDriver.Navigate().Back();
        }

        public static void Refresh()
        {
            WebDriver.Navigate().Refresh();
        }


        public static object ExecuteJavaScript(string javaScript, params object[] args)
        {
            var javaScriptExecutor = (IJavaScriptExecutor)WebDriver;

            return javaScriptExecutor.ExecuteScript(javaScript, args);
        }


        public static void KeyDown(string key)
        {
            new Actions(WebDriver).KeyDown(key);
        }

        public static void KeyUp(string key)
        {
            new Actions(WebDriver).KeyUp(key);
        }

        public static void AlertAccept()
        {
            Thread.Sleep(2000);
            WebDriver.SwitchTo().Alert().Accept();
            WebDriver.SwitchTo().DefaultContent();
        }

        public static void AcceptAlert()
        {
            var accept = WaitHelper.MakeTry(() => WebDriver.SwitchTo().Alert().Accept());
            WaitHelper.SpinWait(accept, TimeSpan.FromSeconds(5));
        }

        #endregion

        #region StartWebDriverPart          
        /* 
        * This Part contains public methods to start and end Browser session
        * If you want to use else WebDriver you can do it by Create new 
        * Start<BrowserName> method.
        * Also you can create ney methods for existing browsers, with settings, which you need
        */

        public static void Start()
        {
            _webDriver = StartWebDriver();
        }

        public static void Quit()
        {
            if (_webDriver == null) return;

            _webDriver.Quit();
            _webDriver = null;
        }

        private static IWebDriver _webDriver;

        private static string _mainWindowHandler;

        private static IWebDriver WebDriver
        {
            get { return _webDriver ?? StartWebDriver(); }
        }

        private static IWebDriver StartWebDriver()
        {
            Contract.Ensures(Contract.Result<IWebDriver>() != null);

            if (_webDriver != null) return _webDriver;

            switch (SelectedBrowser)
            {

                case Browsers.Chrome:
                    _webDriver = StartChrome();
                    break;

                case Browsers.Firefox:
                    _webDriver = StartFirefox();
                    break;

                default:
                    throw new Exception(string.Format("Unknown browser selected: {0}.", SelectedBrowser));
            }

            _webDriver.Manage().Window.Maximize();
            _mainWindowHandler = _webDriver.CurrentWindowHandle;

            return WebDriver;
        }

        private static FirefoxDriver StartFirefox()
        {
            var firefoxProfile = new FirefoxProfile
            {
                AcceptUntrustedCertificates = true,
                EnableNativeEvents = true
            };

            return new FirefoxDriver(firefoxProfile);
        }

        private static ChromeDriver StartChrome()
        {
            var chromeOptions = new ChromeOptions();
            var defaultDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\..\Local\Google\Chrome\User Data\Default";

            if (Directory.Exists(defaultDataFolder))
            {
                WaitHelper.Try(() => DirectoryHelper.ForceDelete(defaultDataFolder));
            }

            return new ChromeDriver(Directory.GetCurrentDirectory(), chromeOptions);
        }

        private static ChromeDriver StartChrome(ChromeOptions chromeOptions)
        {
            return new ChromeDriver(Directory.GetCurrentDirectory(), chromeOptions);
        }

        private static ChromeDriver StartChrome(ChromeOptions chromeOptions, string directory)
        {
            return new ChromeDriver(directory, chromeOptions);
        }  // you can use this method if you need to start ChromeDriver From Enother Diretory


        #endregion


        #region RarelyUsedMethods
        /*
         * Specific Methods which you will never use in Test, i promise.
         * Okay-Okay, Maybe, Sometimes, you are really will need this.
         */

        public static void SwitchToFrame(IWebElement inlineFrame)
        {
            WebDriver.SwitchTo().Frame(inlineFrame);
        }

        public static void SwitchToPopupWindow()
        {
            foreach (var handle in WebDriver.WindowHandles.Where(handle => handle != _mainWindowHandler)) // TODO:
            {
                WebDriver.SwitchTo().Window(handle);
            }
        }

        public static void SwitchToMainWindow()
        {
            WebDriver.SwitchTo().Window(_mainWindowHandler);
        }

        public static void SwitchToDefaultContent()
        {
            WebDriver.SwitchTo().DefaultContent();
        }

        public static void DragAndDrop(IWebElement source, IWebElement destination)
        {
            (new Actions(WebDriver)).DragAndDrop(source, destination).Build().Perform();
        }
        #endregion

    }
}
