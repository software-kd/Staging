using Microsoft.Ajax.Utilities;
using System;
using System.Web.Mvc;

namespace Mayflower.Helpers
{
    public static class OptimizationExtensions
    {
        public static MvcHtmlString CssMinify(this HtmlHelper helper, Func<object, object> markup)
        {
            string notMinifiedCss = (markup.DynamicInvoke(helper.ViewContext) ?? "").ToString();
            var minifier = new Minifier();
            var minifiedJs = minifier.MinifyStyleSheet(notMinifiedCss);
            return new MvcHtmlString(minifiedJs);
        }

        public static MvcHtmlString JsMinify(this HtmlHelper helper, Func<object, object> markup)
        {
            string notMinifiedJs = (markup.DynamicInvoke(helper.ViewContext) ?? "").ToString();
            var minifier = new Minifier();
            var minifiedJs = minifier.MinifyJavaScript(notMinifiedJs);
            return new MvcHtmlString(minifiedJs);
        }
    }
}