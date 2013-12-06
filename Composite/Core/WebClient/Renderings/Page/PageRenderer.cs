﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Xml.Linq;
using Composite.Core.Caching;
using Composite.Core.Extensions;
using Composite.Data;
using Composite.Data.Types;
using Composite.Functions;
using Composite.Core.Instrumentation;
using Composite.Core.Localization;
using Composite.Core.Parallelization;
using Composite.Core.WebClient.Renderings.Template;
using Composite.Core.Xml;
using Composite.C1Console.Security;


namespace Composite.Core.WebClient.Renderings.Page
{
    /// <summary>    
    /// </summary>
    /// <exclude />
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)] 
    public static class PageRenderer
    {
        private static readonly string LogTitle = typeof (PageRenderer).Name;
        private static readonly NameBasedAttributeComparer _nameBasedAttributeComparer = new NameBasedAttributeComparer();


        /// <exclude />
        public static FunctionContextContainer GetPageRenderFunctionContextContainer()
        {
            XEmbeddedControlMapper mapper = new XEmbeddedControlMapper();

            FunctionContextContainer contextContainer = new FunctionContextContainer();
            contextContainer.XEmbedableMapper = mapper;
            contextContainer.SuppressXhtmlExceptions = true;

            return contextContainer;
        }



        /// <exclude />
        public static Control Render(this IPage page, IEnumerable<IPagePlaceholderContent> placeholderContents, FunctionContextContainer functionContextContainer)
        {
            Verify.ArgumentNotNull(page, "page");
            Verify.ArgumentNotNull(functionContextContainer, "functionContextContainer");
            Verify.ArgumentCondition((functionContextContainer.XEmbedableMapper as XEmbeddedControlMapper) != null,
                "functionContextContainer", "Unknown or missing XEmbedable mapper on context container. Use GetPageRenderFunctionContextContainer().");

            CurrentPage = page;

            using (GlobalInitializerFacade.CoreIsInitializedScope)
            {
                string url;

                PageStructureInfo.TryGetPageUrl(page.Id, out url);

                using (TimerProfilerFacade.CreateTimerProfiler(url ?? "(no url)"))
                {
                    var cultureInfo = page.DataSourceId.LocaleScope;
                    System.Threading.Thread.CurrentThread.CurrentCulture = cultureInfo;
                    System.Threading.Thread.CurrentThread.CurrentUICulture = cultureInfo;

                    XEmbeddedControlMapper mapper = (XEmbeddedControlMapper)functionContextContainer.XEmbedableMapper;

                    XDocument document = TemplateInfo.GetTemplateDocument(page.TemplateId);

                    ResolvePlaceholders(document, placeholderContents);

                    Control c = Render(document, functionContextContainer, mapper, page);

                    return c;
                }
            }
        }

        /// <exclude />
        public static XhtmlDocument ParsePlaceholderContent(IPagePlaceholderContent placeholderContent)
        {
            if(placeholderContent == null || string.IsNullOrEmpty(placeholderContent.Content))
            {
                return new XhtmlDocument();
            }

            if (placeholderContent.Content.StartsWith("<html"))
            {
                try
                {
                    return XhtmlDocument.Parse(placeholderContent.Content);
                }
                catch (Exception) { }
            }

            return XhtmlDocument.Parse("<html xmlns='{0}'><head/><body>{1}</body></html>".FormatWith(Namespaces.Xhtml, placeholderContent.Content));
        }

        private static void ResolvePlaceholders(XDocument document, IEnumerable<IPagePlaceholderContent> placeholderContents)
        {
            using (TimerProfilerFacade.CreateTimerProfiler())
            {
                List<XElement> placeHolders = document.Descendants(RenderingElementNames.PlaceHolder).ToList();

                if (placeHolders.Any())
                {
                    foreach (XElement placeHolder in placeHolders.Where(f => f.Attribute(RenderingElementNames.PlaceHolderIdAttribute) != null))
                    {
                        IPagePlaceholderContent placeHolderContent = 
                            placeholderContents
                            .FirstOrDefault(f => f.PlaceHolderId == placeHolder.Attribute(RenderingElementNames.PlaceHolderIdAttribute).Value);

                        string placeHolderId = null;

                        XAttribute idAttribute = placeHolder.Attribute("id");
                        if (idAttribute != null)
                        {
                            placeHolderId = idAttribute.Value;
                            idAttribute.Remove();
                        }

                        XhtmlDocument xhtmlDocument = ParsePlaceholderContent(placeHolderContent);
                        placeHolder.ReplaceWith(xhtmlDocument.Root);


                        if (placeHolderId != null)
                        {
                            try
                            {
                                placeHolder.Add(new XAttribute("id", placeHolderId));
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidOperationException(string.Format("Failed to set id '{0}' on element", placeHolderId), ex);
                            }
                        }
                    }
                }
            }
        }



        /// <exclude />
        public static Control Render(this IPage page, IEnumerable<IPagePlaceholderContent> placeholderContents)
        {
            return page.Render(placeholderContents, GetPageRenderFunctionContextContainer());
        }



        /// <exclude />
        public static Guid CurrentPageId
        {
            get
            {
                if (!RequestLifetimeCache.HasKey("PageRenderer.IPage"))
                {
                    return Guid.Empty;
                }

                return RequestLifetimeCache.TryGet<IPage>("PageRenderer.IPage").Id;
            }
        }



        /// <exclude />
        public static IPage CurrentPage
        {
            get
            {
                if (!RequestLifetimeCache.HasKey("PageRenderer.IPage"))
                {
                    return null;
                }
                
                return RequestLifetimeCache.TryGet<IPage>("PageRenderer.IPage");
            }
            set
            {
                var currentValue = CurrentPage;
                if (currentValue == value)
                {
                    return;
                }

                Verify.IsNull(currentValue, "CurrentPage is already set");

                RequestLifetimeCache.Add("PageRenderer.IPage", value);
            }
        }



        /// <exclude />
        public static CultureInfo CurrentPageCulture
        {
            get
            {
                if (!RequestLifetimeCache.HasKey("PageRenderer.IPage"))
                {
                    return null;
                }

                var page = RequestLifetimeCache.TryGet<IPage>("PageRenderer.IPage");
                return page.DataSourceId.LocaleScope;
            }
        }



        /// <exclude />
        [Obsolete()]
        public static IEnumerable<IData> GetCurrentPageAssociatedData(Type type)
        {
            return PageRenderer.CurrentPage.GetReferees(type);
        }



        /// <exclude />
        [Obsolete()]
        public static IEnumerable<IData> GetCurrentPageAssociatedData<T>() where T : IData
        {
            foreach (IData data in PageRenderer.CurrentPage.GetReferees(typeof(T)))
            {
                yield return (T)data;
            }
        }


        /// <exclude />
        public static Control Render(XDocument document, FunctionContextContainer contextContainer, IXElementToControlMapper mapper, IPage page)
        {
            using (TimerProfilerFacade.CreateTimerProfiler())
            {
                ExecuteEmbeddedFunctions(document.Root, contextContainer);

                ResolvePageFields(document, page);

                NormalizeAspNetForms(document);

                if (document.Root.Name != Namespaces.Xhtml + "html")
                {
                    return new LiteralControl(document.ToString());
                }
                
                XhtmlDocument xhtmlDocument = new XhtmlDocument(document);
                NormalizeXhtmlDocument(xhtmlDocument);

                ResolveRelativePaths(xhtmlDocument);

                AppendC1MetaTags(page, xhtmlDocument);

                LocalizationParser.Parse(xhtmlDocument);

                return xhtmlDocument.AsAspNetControl(mapper);
            }
        }



        /// <summary>
        /// Appends the c1 meta tags to the head section. Those tag are used later on by SEO assistant.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="xhtmlDocument">The XHTML document.</param>
        public static void AppendC1MetaTags(IPage page, XhtmlDocument xhtmlDocument)
        {
            if (UserValidationFacade.IsLoggedIn())
            {
                bool emitMenuTitleMetaTag = string.IsNullOrEmpty(page.MenuTitle) == false;
                bool emitUrlTitleMetaTag = string.IsNullOrEmpty(page.UrlTitle) == false;

                if (emitMenuTitleMetaTag || emitUrlTitleMetaTag)
                {
                    xhtmlDocument.Head.Add(
                        new XComment("The C1.* meta tags are only emitted when you are logged in"),
                        new XElement(Namespaces.Xhtml + "link",
                            new XAttribute("rel", "schema.C1"),
                            new XAttribute("href", "http://www.composite.net/ns/c1/seoassistant")));

                    if (emitMenuTitleMetaTag)
                    {
                        xhtmlDocument.Head.Add(
                            new XElement(Namespaces.Xhtml + "meta",
                                new XAttribute("name", "C1.menutitle"),
                                new XAttribute("content", page.MenuTitle)));
                    }

                    if (emitUrlTitleMetaTag)
                    {
                        xhtmlDocument.Head.Add(
                            new XElement(Namespaces.Xhtml + "meta",
                                new XAttribute("name", "C1.urltitle"),
                                new XAttribute("content", page.UrlTitle)));
                    }
                }
            }
        }


        /// <exclude />
        public static void ResolveRelativePaths(XhtmlDocument xhtmlDocument)
        {
            IEnumerable<XElement> xhtmlElements = xhtmlDocument.Descendants().Where(f => f.Name.Namespace == Namespaces.Xhtml);
            IEnumerable<XAttribute> pathAttributes = xhtmlElements.Attributes().Where(f => f.Name.LocalName == "src" || f.Name.LocalName == "href" || f.Name.LocalName == "action");

            string applicationVirtualPath = UrlUtils.PublicRootPath;

            List<XAttribute> relativePathAttributes = pathAttributes.Where(f => f.Value.StartsWith("~/") || f.Value.StartsWith("%7E/")).ToList();

            foreach (XAttribute relativePathAttribute in relativePathAttributes)
            {
                int tildePrefixLength = (relativePathAttribute.Value.StartsWith("~") ? 1 : 3);
                relativePathAttribute.Value = applicationVirtualPath + relativePathAttribute.Value.Substring(tildePrefixLength);
            }

            if (applicationVirtualPath.Length>1)
            {
                List<XAttribute> hardRootedPathAttributes = pathAttributes.Where(f => f.Value.StartsWith("/Renderers/")).ToList();

                foreach (XAttribute hardRootedPathAttribute in hardRootedPathAttributes)
                {
                    hardRootedPathAttribute.Value = applicationVirtualPath + hardRootedPathAttribute.Value;
                }
            }


        }



        private static void NormalizeAspNetForms(XDocument document)
        {
            List<XElement> aspNetFormElements = document.Descendants(Namespaces.AspNetControls + "form").Reverse().ToList();

            foreach (XElement aspNetFormElement in aspNetFormElements)
            {
                if (aspNetFormElement.Ancestors(Namespaces.AspNetControls + "form").Any())
                {
                    aspNetFormElement.ReplaceWith(aspNetFormElement.Nodes());
                }
            }

        }


        /// <exclude />
        public static void ResolvePageFields(XDocument document, IPage page)
        {
            foreach (XElement elem in document.Descendants(RenderingElementNames.PageTitle).ToList())
            {
                elem.ReplaceWith(page.Title);
            }

            foreach (XElement elem in document.Descendants(RenderingElementNames.PageAbstract).ToList())
            {
                elem.ReplaceWith(page.Description);
            }


            foreach (XElement elem in document.Descendants(RenderingElementNames.PageMetaTagDescription).ToList())
            {
                if (string.IsNullOrEmpty(page.Description) == false)
                {
                    elem.ReplaceWith(
                        new XElement(Namespaces.Xhtml + "meta",
                            new XAttribute("name", "description"),
                            new XAttribute("content", page.Description)));
                }
                else
                {
                    elem.Remove();
                }
            }

        }



        /// <exclude />
        public static void ExecuteEmbeddedFunctions(XElement element, FunctionContextContainer contextContainer)
        {
            using (TimerProfilerFacade.CreateTimerProfiler())
            {
                IEnumerable<XElement> functionCallDefinitions = element.DescendantsAndSelf(Namespaces.Function10 + "function")
                                                                       .Where(f => !f.Ancestors(Namespaces.Function10 + "function").Any());

                var functionCalls = functionCallDefinitions.ToList();
                if (functionCalls.Count == 0) return;

                object[] functionExecutionResults = new object[functionCalls.Count];

                ParallelFacade.For("PageRenderer. Embedded function execution", 0, functionCalls.Count, i =>
                {
                    XElement functionCallDefinition = functionCalls[i];
                    string functionName = null;

                    object functionResult;
                    try
                    {
                        // Evaluating function calls in parameters
                        IEnumerable<XElement> parameters = functionCallDefinition.Elements();

                        foreach (XElement parameterNode in parameters)
                        {
                            ExecuteEmbeddedFunctions(parameterNode, contextContainer);
                        }


                        // Executing a function call
                        BaseRuntimeTreeNode runtimeTreeNode = FunctionTreeBuilder.Build(functionCallDefinition);

                        functionName = runtimeTreeNode.GetAllSubFunctionNames().FirstOrDefault();

                        object result = runtimeTreeNode.GetValue(contextContainer);

                        if (result != null)
                        {
                            // Evaluating functions in a result of a function call
                            object embedableResult = contextContainer.MakeXEmbedable(result);

                            foreach (XElement xelement in GetXElements(embedableResult))
                            {
                                ExecuteEmbeddedFunctions(xelement, contextContainer);
                            }

                            functionResult = embedableResult;
                        }
                        else
                        {
                            functionResult = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        using (Profiler.Measure("PageRenderer. Loggin an exception"))
                        {
                            XElement errorBoxHtml;

                            if (!contextContainer.ProcessException(functionName, ex, LogTitle, out errorBoxHtml))
                            {
                                throw;
                            }

                            functionResult = errorBoxHtml;
                        }
                    }

                    functionExecutionResults[i] = functionResult;
                });

                // Applying changes
                for(int i=0; i < functionCalls.Count; i++)
                {
                    XElement functionCall = functionCalls[i];
                    object functionCallResult = functionExecutionResults[i];
                    if(functionCallResult != null)
                    {
                        if (functionCallResult is XAttribute && functionCall.Parent != null)
                        {
                            functionCall.Parent.Add(functionCallResult);
                            functionCall.Remove();
                        }
                        else
                        {
                            functionCall.ReplaceWith(functionCallResult);
                        }
                    }
                    else
                    {
                        functionCall.Remove();
                    }
                }
            }
        }



        private static IEnumerable<XElement> GetXElements(object source)
        {
            if (source is XElement)
            {
                yield return (XElement)source;
            }

            if (source is IEnumerable<XElement>)
            {
                foreach (XElement xelement in (IEnumerable<XElement>)source)
                {
                    yield return xelement;
                }
            }
        }



        private class NameBasedAttributeComparer : IEqualityComparer<XAttribute>
        {
            public bool Equals(XAttribute x, XAttribute y)
            {
                return x.Name == y.Name;
            }

            public int GetHashCode(XAttribute obj)
            {
                return obj.Name.GetHashCode();
            }
        }

        /// <exclude />
        public static void NormalizeXhtmlDocument(XhtmlDocument rootDocument)
        {
            using (TimerProfilerFacade.CreateTimerProfiler())
            {
                XElement nestedDocument = rootDocument.Root.Descendants(Namespaces.Xhtml + "html").FirstOrDefault();

                while (nestedDocument != null)
                {
                    XhtmlDocument nestedXhtml = new XhtmlDocument(nestedDocument);

                    rootDocument.Root.Add(nestedXhtml.Root.Attributes().Except(rootDocument.Root.Attributes(), _nameBasedAttributeComparer));
                    rootDocument.Head.Add(nestedXhtml.Head.Nodes());
                    rootDocument.Head.Add(nestedXhtml.Head.Attributes().Except(rootDocument.Head.Attributes(), _nameBasedAttributeComparer));
                    rootDocument.Body.Add(nestedXhtml.Body.Attributes().Except(rootDocument.Body.Attributes(), _nameBasedAttributeComparer));

                    nestedDocument.ReplaceWith(nestedXhtml.Body.Nodes());

                    nestedDocument = rootDocument.Root.Descendants(Namespaces.Xhtml + "html").FirstOrDefault();
                }
            }
        }



        /// <exclude />
        public static bool DisableAspNetPostback(Control c)
        {
            bool formDisabled = false;
            DisableAspNetPostback(c, out formDisabled);
            return formDisabled;
        }


        private static void DisableAspNetPostback(Control c, out bool formDisabled)
        {
            formDisabled = false;

            if (c is HtmlForm)
            {
                ((HtmlForm)c).Attributes.Add("onsubmit", "alert('Postback disabled in preview mode'); return false;");
                formDisabled = true;
                return;
            }

            if (c is HtmlHead)
            {
                return;
            }

            foreach (Control child in c.Controls)
            {
                DisableAspNetPostback(child, out formDisabled);
                if (formDisabled) break;
            }
        }
    }
}
