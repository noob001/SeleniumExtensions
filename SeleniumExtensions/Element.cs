using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using OpenQA.Selenium;

namespace SeleniumExtensions
{
    public partial class WebElement
    {
        private By _firstSelector;
        private IList<IWebElement> _searchCache;

        private IWebElement FindSingle()
        {
            return TryFindSingle();
        }

        private IWebElement TryFindSingle()
        {
            Contract.Ensures(Contract.Result<IWebElement>() != null);

            try
            {
                return FindSingleIWebElement();
            }
            catch (StaleElementReferenceException)
            {
                ClearSearchResultCache();

                return FindSingleIWebElement();
            }
            catch (InvalidSelectorException)
            {
                throw;
            }
            catch (WebDriverException)
            {
                throw;
            }
            catch (WebElementNotFoundException)
            {
                throw;
            }
            catch
            {
                throw WebElementNotFoundException;
            }
        }

        private IWebElement FindSingleIWebElement()
        {
            var elements = FindIWebElements();

            if (!elements.Any()) throw WebElementNotFoundException;

            var element = elements.Count() == 1
                ? elements.Single()
                : _index == -1
                    ? elements.Last()
                    : elements.ElementAt(_index);
            // ReSharper disable UnusedVariable
            var elementAccess = element.Enabled;
            // ReSharper restore UnusedVariable

            return element;
        }



        private IList<IWebElement> FindIWebElements()
        {
            if (_searchCache != null)
            {
                return _searchCache;
            }

            Browser.WaitReadyState();

            var resultEnumerable = Browser.FindElements(_firstSelector);

            try
            {
                resultEnumerable = FilterByVisibility(resultEnumerable).ToList();
                resultEnumerable = FilterByTagNames(resultEnumerable).ToList();
                resultEnumerable = FilterByText(resultEnumerable).ToList();
                resultEnumerable = FilterByTagAttributes(resultEnumerable).ToList();
                resultEnumerable = resultEnumerable.ToList();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                return new List<IWebElement>();
            }

            var resultList = resultEnumerable.ToList();

            return resultList;
        }

        private WebElementNotFoundException WebElementNotFoundException
        {
            get
            {
                CheckConnectionFailure();

                return new WebElementNotFoundException(string.Format("Can't find single element with given search criteria: {0}.",
                    SearchCriteriaToString()));
            }
        }

        private static void CheckConnectionFailure()
        {
            const string connectionFailure = "connectionFailure";

            Contract.Assert(!Browser.PageSource.Contains(connectionFailure),
                "Connection can't be established.");
        }
    }
}
