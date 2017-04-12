using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Threading;
using SeleniumExtensions.Tags;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SeleniumExtensions
{
    #region WebActions

    internal enum SelectTypes
    {
        ByValue,
        ByText
    }

    public partial class WebElement
    {
        #region Common properties

        public int Count
        {
            get { return FindIWebElements().Count; }
        }

        public bool Enabled
        {
            get { return FindSingle().Enabled; }
        }

        public bool Displayed
        {
            get { return FindSingle().Displayed; }
        }

        public bool Selected
        {
            get { return FindSingle().Selected; }
        }

        public string Text
        {
            set
            {
                var element = FindSingle();

                if (element.TagName == EnumHelper.GetEnumDescription(TagNames.Input) || element.TagName == EnumHelper.GetEnumDescription(TagNames.TextArea))
                {
                    element.Clear();
                }
                else
                {
                    element.SendKeys(Keys.LeftControl + "a");
                    element.SendKeys(Keys.Delete);
                }

                if (string.IsNullOrEmpty(value)) return;

                Browser.ExecuteJavaScript(string.Format("arguments[0].value = \"{0}\";", value), element);

                WaitHelper.Try(() => FireJQueryEvent(JavaScriptEvents.KeyUp));
            }
            get
            {
                var element = FindSingle();

                return !string.IsNullOrEmpty(element.Text) ? element.Text : element.GetAttribute(EnumHelper.GetEnumDescription(TagAttributes.Value));
            }
        }

        public int TextInt
        {
            set { Text = value.ToString(CultureInfo.InvariantCulture); }
            get { return Text.ToInt(); }
        }

        public string InnerHtml
        {
            get { return Browser.ExecuteJavaScript("return arguments[0].innerHTML;", FindSingle()).ToString(); }
        }

        #endregion

        #region Common methods

        public bool Exists()
        {
            return FindIWebElements().Any();
        }

        public bool Exists(TimeSpan timeSpan)
        {
            return WaitHelper.SpinWait(Exists, timeSpan, TimeSpan.FromMilliseconds(200));
        }

        public bool Exists(int seconds)
        {
            return WaitHelper.SpinWait(Exists, TimeSpan.FromSeconds(seconds), TimeSpan.FromMilliseconds(200));
        }

        public void Click(bool useJQuery = true)
        {
            var element = FindSingle();

            Contract.Assert(element.Enabled);

            if (useJQuery && element.TagName != EnumHelper.GetEnumDescription(TagNames.Link))
            {
                FireJQueryEvent(element, JavaScriptEvents.Click);
            }
            else
            {
                try
                {
                    element.Click();
                }
                catch (InvalidOperationException e)
                {
                    if (e.Message.Contains("Element is not clickable"))
                    {
                        Thread.Sleep(2000);
                        element.Click();
                    }
                }
            }
        }

        public void SendKeys(string keys)
        {
            FindSingle().SendKeys(keys);
        }

        public void SetCheck(bool value, bool useJQuery = true)
        {
            var element = FindSingle();

            Contract.Assert(element.Enabled);

            const int tryCount = 10;

            for (var i = 0; i < tryCount; i++)
            {
                element = FindSingle();

                Set(value, useJQuery);

                if (element.Selected == value)
                {
                    return;
                }
            }

            Contract.Assert(element.Selected == value);
        }

        public void Select(string optionValue)
        {
            SelectCommon(optionValue, SelectTypes.ByValue);
        }

        public void Select(int optionValue)
        {
            SelectCommon(optionValue.ToString(CultureInfo.InvariantCulture), SelectTypes.ByValue);
        }

        public void SelectByText(string optionText)
        {
            SelectCommon(optionText, SelectTypes.ByText);
        }

        public string GetAttribute(TagAttributes tagAttribute)
        {
            return FindSingle().GetAttribute(EnumHelper.GetEnumDescription(tagAttribute));
        }

        #endregion

        #region Additional methods


        public void CacheSearchResult()
        {
            _searchCache = FindIWebElements();
        }

        public void ClearSearchResultCache()
        {
            _searchCache = null;
        }

        public void FireJQueryEvent(JavaScriptEvents javaScriptEvent)
        {
            var element = FindSingle();

            FireJQueryEvent(element, javaScriptEvent);
        }

        public void ForEach(Action<WebElement> action)
        {
            Contract.Requires(action != null);

            CacheSearchResult();

            Enumerable.Range(0, Count).ToList().ForEach(i => action(ByIndex(i)));

            ClearSearchResultCache();
        }

        public List<T> Select<T>(Func<WebElement, T> action)
        {
            Contract.Requires(action != null);

            var result = new List<T>();

            ForEach(e => result.Add(action(e)));

            return result;
        }

        public List<WebElement> Where(Func<WebElement, bool> action)
        {
            Contract.Requires(action != null);

            var result = new List<WebElement>();

            ForEach(e =>
            {
                if (action(e)) result.Add(e);
            });

            return result;
        }

        public WebElement Single(Func<WebElement, bool> action)
        {
            return Where(action).Single();
        }

        #endregion

        #region Helpers

        private void Set(bool value, bool useJQuery = true)
        {
            if (Selected ^ value)
            {
                Click(useJQuery);
            }
        }

        private void SelectCommon(string option, SelectTypes selectType)
        {
            Contract.Requires(!string.IsNullOrEmpty(option));

            var element = FindSingle();

            Contract.Assert(element.Enabled);

            switch (selectType)
            {
                case SelectTypes.ByValue:
                    new SelectElement(element).SelectByValue(option);
                    return;
                case SelectTypes.ByText:
                    new SelectElement(element).SelectByText(option);
                    return;
                default:
                    throw new Exception(string.Format("Unknown select type: {0}.", selectType));
            }
        }

        private void FireJQueryEvent(IWebElement element, JavaScriptEvents javaScriptEvent)
        {
            var eventName = EnumHelper.GetEnumDescription(javaScriptEvent);

            Browser.ExecuteJavaScript(string.Format("$(arguments[0]).{0}();", eventName), element);
        }

        #endregion
    }

    public enum JavaScriptEvents
    {
        [Description("keyup")]
        KeyUp,

        [Description("click")]
        Click
    }

    #endregion

    #region ByCriteria

    internal class SearchProperty
    {
        public string AttributeName { get; set; }
        public string AttributeValue { get; set; }
        public bool ExactMatch { get; set; }
    }

    internal class TextSearchData
    {
        public string Text { get; set; }
        public bool ExactMatch { get; set; }
    }

    public partial class WebElement
    {
        private readonly IList<SearchProperty> _searchProperties = new List<SearchProperty>();
        private readonly IList<TagNames> _searchTags = new List<TagNames>();
        private bool _searchHidden;
        private int _index;
        private string _xPath;
        private TextSearchData _textSearchData;

        public WebElement ByAttribute(TagAttributes tagAttribute, string attributeValue, bool exactMatch = true)
        {
            return ByAttribute(EnumHelper.GetEnumDescription(tagAttribute), attributeValue, exactMatch);
        }

        public WebElement ByAttribute(TagAttributes tagAttribute, int attributeValue, bool exactMatch = true)
        {
            return ByAttribute(EnumHelper.GetEnumDescription(tagAttribute), attributeValue.ToString(), exactMatch);
        }

        public WebElement ById(string id, bool exactMatch = true)
        {
            return ByAttribute(TagAttributes.Id, id, exactMatch);
        }

        public WebElement ById(int id, bool exactMatch = true)
        {
            return ByAttribute(TagAttributes.Id, id.ToString(), exactMatch);
        }

        public WebElement ByName(string name, bool exactMatch = true)
        {
            return ByAttribute(TagAttributes.Name, name, exactMatch);
        }

        public WebElement ByClass(string className, bool exactMatch = true)
        {
            return ByAttribute(TagAttributes.Class, className, exactMatch);
        }

        public WebElement ByTitle(string title, bool exactMatch = true)
        {
            return ByAttribute(TagAttributes.Title, title, exactMatch);
        }

        public WebElement ByTagName(TagNames tagName)
        {
            var selector = By.TagName(EnumHelper.GetEnumDescription(tagName));

            _firstSelector = _firstSelector ?? selector;
            _searchTags.Add(tagName);

            return this;
        }

        public WebElement ByXPath(string xPath)
        {
            Contract.Assume(_firstSelector == null,
                "XPath can be only the first search criteria.");

            _firstSelector = By.XPath(xPath);
            _xPath = xPath;

            return this;
        }

        public WebElement ByIndex(int index)
        {
            _index = index;

            return this;
        }

        public WebElement ByHref(string href, bool exactMatch = true)
        {
            return ByAttribute(TagAttributes.Href, href, exactMatch);
        }

        public WebElement ByType(string type, bool exactMatch = true)
        {
            return ByAttribute(TagAttributes.Type, type, exactMatch);
        }

        public WebElement First()
        {
            _index = 0;

            return this;
        }

        public WebElement Last()
        {
            _index = -1;

            return this;
        }

        public WebElement IncludeHidden()
        {
            _searchHidden = true;

            return this;
        }

        public WebElement ByText(string text, bool exactMatch = true)
        {
            var selector = exactMatch ?
                By.XPath(string.Format("//*[text()=\"{0}\"]", text)) :
                By.XPath(string.Format("//*[contains(text(), \"{0}\")]", text));

            _firstSelector = _firstSelector ?? selector;
            _textSearchData = new TextSearchData { Text = text, ExactMatch = exactMatch };

            return this;
        }

        private WebElement ByAttribute(string tagAttribute, string attributeValue, bool exactMatch = true)
        {
            var xPath = exactMatch ?
                        string.Format("//*[@{0}=\"{1}\"]", tagAttribute, attributeValue) :
                        string.Format("//*[contains(@{0}, \"{1}\")]", tagAttribute, attributeValue);
            var selector = By.XPath(xPath);

            _firstSelector = _firstSelector ?? selector;

            _searchProperties.Add(new SearchProperty
            {
                AttributeName = tagAttribute,
                AttributeValue = attributeValue,
                ExactMatch = exactMatch
            });

            return this;
        }

        private string SearchCriteriaToString()
        {
            var result = _searchProperties.Select(searchProperty =>
                string.Format("{0}: {1} ({2})",
                    searchProperty.AttributeName,
                    searchProperty.AttributeValue,
                    searchProperty.ExactMatch ? "exact" : "contains")).ToList();

            result.AddRange(_searchTags.Select(searchTag =>
                string.Format("tag: {0}", searchTag)));

            if (_xPath != null)
            {
                result.Add(string.Format("XPath: {0}", _xPath));
            }

            if (_textSearchData != null)
            {
                result.Add(string.Format("text: {0} ({1})",
                    _textSearchData.Text,
                    _textSearchData.ExactMatch ? "exact" : "contains"));
            }

            return string.Join(", ", result);
        }
    }
    #endregion

    #region WebElementException
    public class WebElementNotFoundException : Exception
    {
        public WebElementNotFoundException(string message) : base(message)
        {
        }
    }

    #endregion

    #region WebElementFilters
    public partial class WebElement
    {
        private IEnumerable<IWebElement> FilterByVisibility(IEnumerable<IWebElement> result)
        {
            return !_searchHidden ? result.Where(item => item.Displayed) : result;
        }

        private IEnumerable<IWebElement> FilterByTagNames(IEnumerable<IWebElement> elements)
        {
            return _searchTags.Aggregate(elements, (current, tag) => current.Where(item => item.TagName == EnumHelper.GetEnumDescription(tag)));
        }

        private IEnumerable<IWebElement> FilterByText(IEnumerable<IWebElement> result)
        {
            if (_textSearchData != null)
            {
                result = _textSearchData.ExactMatch
                    ? result.Where(item => item.Text == _textSearchData.Text)
                    : result.Where(item => item.Text.Contains(_textSearchData.Text, StringComparison.InvariantCultureIgnoreCase));
            }

            return result;
        }

        private IEnumerable<IWebElement> FilterByTagAttributes(IEnumerable<IWebElement> elements)
        {
            return _searchProperties.Aggregate(elements, FilterByTagAttribute);
        }

        private static IEnumerable<IWebElement> FilterByTagAttribute(IEnumerable<IWebElement> elements, SearchProperty searchProperty)
        {
            return searchProperty.ExactMatch ?
                elements.Where(item => item.GetAttribute(searchProperty.AttributeName) != null && item.GetAttribute(searchProperty.AttributeName).Equals(searchProperty.AttributeValue)) :
                elements.Where(item => item.GetAttribute(searchProperty.AttributeName) != null && item.GetAttribute(searchProperty.AttributeName).Contains(searchProperty.AttributeValue));
        }
    }
    #endregion
}
