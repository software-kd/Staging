using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using System.Web.Mvc;

namespace Mayflower.General
{
    public sealed class AcceptCustomButtonAttribute : ActionMethodSelectorAttribute
    {
        #region Private Members
        private string _buttonName;
        private string _buttonValue;
        #endregion

        #region Public Properties
        /// <summary>
        /// Holds the value of the Name attribute of the HTML submit button
        /// </summary>
        public string Name
        {
            get { return _buttonName; }
            set { _buttonName = value; }
        }

        /// <summary>
        /// Holds the name in the Value attribute of HTML submit button
        /// </summary>
        public string Value
        {
            get { return _buttonValue; }
            set { _buttonValue = value; }
        }
        #endregion

        #region ActionMethodSelectorAttribute members
        public override bool IsValidForRequest(ControllerContext controllerContext, System.Reflection.MethodInfo methodInfo)
        {
            return controllerContext.HttpContext.Request.Form[this._buttonName] == this._buttonValue;
        }
        #endregion
    }
}