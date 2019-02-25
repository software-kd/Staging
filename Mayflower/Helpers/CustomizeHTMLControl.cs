using Microsoft.Ajax.Utilities;
using System;
using System.Linq.Expressions;
using System.Web.Mvc.Html;
using System.Web.Mvc;

namespace Mayflower.Helpers
{
    public static class CustomizeHTMLControl
    {
        public static MvcHtmlString StyledCheckBoxFor<TModel>(this HtmlHelper<TModel> htmlHelper, Expression<Func<TModel, bool>> expression, string labelMsg = "", string clientId = null)
        {
            if (expression == null)
            {
                throw new ArgumentNullException("expression");
            }

            ModelMetadata metadata = ModelMetadata.FromLambdaExpression(expression, htmlHelper.ViewData);
            
            MvcHtmlString _StyledCheckBox = null;
            string labelText = labelMsg == "" ? (metadata.DisplayName ?? metadata.PropertyName) : labelMsg;

            bool? isChecked = null;
            if (metadata.Model != null)
            {
                bool modelChecked;
                if (Boolean.TryParse(metadata.Model.ToString(), out modelChecked))
                {
                    isChecked = modelChecked;
                }
                _StyledCheckBox = new MvcHtmlString(string.Format(
                            @"<input id='{0}' name='{1}' class='checkbox-custom' type='checkbox' value='true' {3}>
                        <label for='{0}' class='checkbox-custom-label add-cursor-pointer'>{2}</label>
                        <input name='{1}' type='hidden' value='false' />", clientId ?? htmlHelper.ClientIdFor(expression).ToString(), htmlHelper.ClientNameFor(expression), labelText, (bool)metadata.Model ? "checked" : null));
            }
            else
            {
                _StyledCheckBox = new MvcHtmlString(string.Format(
                                @"<input id='{0}' name='{1}' class='checkbox-custom' type='checkbox' value='true'>
                        <label for='{0}' class='checkbox-custom-label add-cursor-pointer'>{2}</label>
                        <input name='{1}' type='hidden' value='false' />", clientId ?? htmlHelper.ClientIdFor(expression).ToString(), htmlHelper.ClientNameFor(expression), labelText));
            }


            return _StyledCheckBox;
        }

        public static MvcHtmlString ClientIdFor<TModel, TProperty>(
            this HtmlHelper<TModel> htmlHelper,
            Expression<Func<TModel, TProperty>> expression)
        {
            return MvcHtmlString.Create(
                  htmlHelper.ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldId(
                      ExpressionHelper.GetExpressionText(expression)));
        }

        public static MvcHtmlString ClientNameFor<TModel, TProperty>(
            this HtmlHelper<TModel> htmlHelper,
            Expression<Func<TModel, TProperty>> expression)
        {
            return MvcHtmlString.Create(
                  htmlHelper.ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(
                      ExpressionHelper.GetExpressionText(expression)));
        }
    }
}