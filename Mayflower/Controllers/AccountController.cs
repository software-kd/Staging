using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using Alphareds.Module.MemberController;
using DotNetOpenAuth.AspNet;
using Microsoft.Web.WebPages.OAuth;
using Newtonsoft.Json;
using NLog;
using Mayflower.Filters;
using Mayflower.General;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Transactions;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using WebMatrix.WebData;
using PagedList;
using Alphareds.Module.CommonController;
using System.Linq.Expressions;
using Alphareds.Module.Cryptography;
using WebGrease.Css.Extensions;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using System.Threading.Tasks;

namespace Mayflower.Controllers
{
    [Authorize]
    [InitializeSimpleMembership]
    public class AccountController : Controller
    {
        private MayFlower db = null;
        private string tripid = Guid.NewGuid().ToString(); // Do not use static, to prevent share betweeen thread.
        private Logger logger = LogManager.GetCurrentClassLogger();

        //private ApplicationSignInManager _signInManager;
        //private ApplicationUserManager _userManager;
        private CustomPrincipal CustomPrincipal => (User as Mayflower.General.CustomPrincipal);

        public AccountController()
        {
            db = new MayFlower();
        }

        /*public AccountController(ApplicationUserManager userManager)
        {
            UserManager = userManager;
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }*/

        public enum StatusMessage
        {
            success,
            error,
            fail
        }

        public enum MessageId
        {
            SuccessCreate,
            SuccessSendResetMail,
            SuccessReset,
            Fail
        }

        #region "User" table login
        //
        // GET: /Account/Login
        [AllowAnonymous]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "None")]
        public ActionResult Login(string returnUrl, string loginerror = "")
        {
            if (Request.IsAuthenticated || User.Identity.IsAuthenticated)
            {
                if (Alphareds.Module.Common.Core.EnableCMS)
                {
                    return RedirectToAction("RedirectAndPOST", "DynamicFormPostSurface", new { destinationUrl = System.Web.Configuration.WebConfigurationManager.AppSettings["AlphaReds.CMSUrl"], type = "SessionTransfer" });
                }
                else
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            else
            {
                string postBackUrl = null;

                if (Request.UrlReferrer != null && string.IsNullOrWhiteSpace(loginerror))
                {
                    postBackUrl = Request.UrlReferrer.AbsoluteUri;
                    Session["RequestLoginURL"] = postBackUrl;
                }

                ViewBag.UserNotAuthorized = loginerror;
                ViewBag.ReturnUrl = returnUrl ?? postBackUrl ?? Session["RequestLoginURL"];
                TempData["loginerr"] = string.Empty;
                return View();
            }
        }

        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [AcceptCustomButton(Name = "btnLogin", Value = "Login")]
        public ActionResult Login(User model, string returnUrl, bool rememberMe = false)
        {
            TempData["loginerr"] = string.Empty;

            Mayflower.General.LoginClass login = new LoginClass(model.Email, model.Pwd, db);
            var userData = login.UserData;

            if (userData.IsValidUser && userData.IsActive) // not system disabled account
            {
                Response.SetAuthCookie(userData.UserId.ToString(), rememberMe, userData);

                if (userData.IsProfileActive)
                {
                    MemberServiceController.UpdateLoginDate(login.User, db);
                }
                else if (!userData.IsProfileActive)
                {
                    // Redirect to profile page, for user request activation email.
                    return RedirectToAction("ManageProfile", "Member");
                }

                string requestLoginURL = string.IsNullOrWhiteSpace(returnUrl) ? Session["RequestLoginURL"]?.ToString() : returnUrl;
                if (Session["RequestLoginURL"] != null)
                {
                    Session.Remove("RequestLoginURL");
                }

                if (!string.IsNullOrWhiteSpace(requestLoginURL))
                {
                    if (Url.IsLocalUrl(requestLoginURL)) // local mean doesn't include hostname, schema, ex "/guest/details/"
                    {
                        return Redirect(requestLoginURL);
                    }

                    string cmsUrl = Alphareds.Module.Common.Core.EnableCMS ?
                        System.Web.Configuration.WebConfigurationManager.AppSettings["AlphaReds.CMSUrl"] : null;

                    UriBuilder reqUriBuilder = new UriBuilder(requestLoginURL);
                    UriBuilder builder = Request.UrlReferrer == null ? new UriBuilder() : new UriBuilder(Request.UrlReferrer);
                    var query = HttpUtility.ParseQueryString(builder.Query);
                    var descQuery = HttpUtility.ParseQueryString(reqUriBuilder.Query);

                    if (query != null && query.Count > 0)
                    {
                        foreach (var item in query)
                        {
                            string _key = item.ToString().ToLower();

                            if (_key != "loginerror" && _key != "returnurl")
                            {
                                descQuery[_key] = query[_key];
                            }
                        }
                    }

                    reqUriBuilder.Query = descQuery.ToString();
                    requestLoginURL = reqUriBuilder.ToString();
                    bool isLogoutPage = reqUriBuilder.Uri.AbsolutePath.ToLower() == "/account/loggedout";
                    requestLoginURL = isLogoutPage ? "~/" : requestLoginURL;

                    if (cmsUrl == null) // Not Enable CMS
                    {
                        return Redirect(requestLoginURL);
                    }
                    else //Enabled CMS
                    {
                        DynamicFormPostSurfaceController dny = new DynamicFormPostSurfaceController();
                        return dny.SecureLogin(userData, requestLoginURL, Request.Form, userData.UserId);
                    }
                }
                else
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            else // login failed
            {
                string errorMsg = string.Empty;

                if (userData.UserId != 0 && !userData.IsActive)
                { errorMsg = "Account disabled, please contact our customer services"; }
                else if (userData.UserId != 0 && userData.IsActive && !userData.IsProfileActive)
                { errorMsg = "Account inactive, please confirm your account activated first"; }
                else { errorMsg = "Incorrect Email & Password"; }
                TempData["loginerr"] = errorMsg;
                if (returnUrl != null && string.IsNullOrWhiteSpace(errorMsg))
                {
                    return Redirect(returnUrl);
                }
                else
                {
                    return RedirectToAction("Login", "Account", new { loginerror = errorMsg, returnUrl });
                }
            }
        }
        #endregion

        //
        // POST: /Account/LogOff

        [HttpPost]
        //[ValidateAntiForgeryToken]
        public ActionResult LogOff(string role = "")
        {
            Session.Abandon();
            Session.Clear();
            Session.RemoveAll();
            WebSecurity.Logout();
            return RedirectToAction("LoggedOut", "Account");
        }

        //
        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult LoggedOut()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult Register()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult PasswordChangeSuccess()
        {
            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Disassociate(string provider, string providerUserId)
        {
            string ownerAccount = OAuthWebSecurity.GetUserName(provider, providerUserId);
            ManageMessageId? message = null;

            // Only disassociate the account if the currently logged in user is the owner
            if (ownerAccount == User.Identity.Name)
            {
                // Use a transaction to prevent the user from deleting their last login credential
                using (var scope = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions { IsolationLevel = IsolationLevel.Serializable }))
                {
                    bool hasLocalAccount = OAuthWebSecurity.HasLocalAccount(WebSecurity.GetUserId(User.Identity.Name));
                    if (hasLocalAccount || OAuthWebSecurity.GetAccountsFromUserName(User.Identity.Name).Count > 1)
                    {
                        OAuthWebSecurity.DeleteAccount(provider, providerUserId);
                        scope.Complete();
                        message = ManageMessageId.RemoveLoginSuccess;
                    }
                }
            }

            return RedirectToAction("ManagePassword", new { Message = message });
        }

        //
        // GET: /Account/ManagePassword

        public ActionResult ManagePassword(ManageMessageId? message)
        {
            #region original code
            //ViewBag.StatusMessage =
            //    message == ManageMessageId.ChangePasswordSuccess ? "Your password has been changed."
            //    : message == ManageMessageId.SetPasswordSuccess ? "Your password has been set."
            //    : message == ManageMessageId.RemoveLoginSuccess ? "The external login was removed."
            //    : "";
            //ViewBag.HasLocalPassword = OAuthWebSecurity.HasLocalAccount(WebSecurity.GetUserId(User.Identity.Name));
            //ViewBag.ReturnUrl = Url.Action("Manage");
            //return View();
            #endregion

            ViewBag.StatusMessage =
                message == ManageMessageId.ChangePasswordSuccess ? "Your password has been changed."
                : message == ManageMessageId.SetPasswordSuccess ? "Your password has been set."
                : message == ManageMessageId.RemoveLoginSuccess ? "The external login was removed."
                : message == ManageMessageId.IncorrectPassword ? "Incorrect old password."
                : "";

            //ViewBag.HasLocalPassword = OAuthWebSecurity.HasLocalAccount(WebSecurity.GetUserId(User.Identity.Name));
            int userID = Convert.ToInt32(User.Identity.Name);

            User user = db.Users.FirstOrDefault(a => a.UserID == userID);
            if (user != null && !string.IsNullOrEmpty(user.Pwd))
            {
                ViewBag.HasLocalPassword = true;
            }
            else
                ViewBag.HasLocalPassword = false;

            ViewBag.ReturnUrl = Url.Action("ManagePassword");
            return View();
        }

        //
        // POST: /Account/ManagePassword

        [HttpPost]
        [NoCache]
        [ValidateAntiForgeryToken]
        [AcceptCustomButton(Name = "btnChangePassword", Value = "Change password")]
        public ActionResult ManagePassword(LocalPasswordModel model)
        {
            #region hide this original code
            //bool hasLocalAccount = OAuthWebSecurity.HasLocalAccount(WebSecurity.GetUserId(User.Identity.Name));
            //ViewBag.HasLocalPassword = hasLocalAccount;
            //ViewBag.ReturnUrl = Url.Action("Manage");
            //if (hasLocalAccount)
            //{
            //    if (ModelState.IsValid)
            //    {
            //        // ChangePassword will throw an exception rather than return false in certain failure scenarios.
            //        bool changePasswordSucceeded;
            //        try
            //        {
            //            changePasswordSucceeded = WebSecurity.ChangePassword(User.Identity.Name, model.OldPassword, model.NewPassword);
            //        }
            //        catch (Exception)
            //        {
            //            changePasswordSucceeded = false;
            //        }

            //        if (changePasswordSucceeded)
            //        {
            //            return RedirectToAction("Manage", new { Message = ManageMessageId.ChangePasswordSuccess });
            //        }
            //        else
            //        {
            //            ModelState.AddModelError("", "The current password is incorrect or the new password is invalid.");
            //        }
            //    }
            //}
            //else
            //{
            //    // User does not have a local password so remove any validation errors caused by a missing
            //    // OldPassword field
            //    ModelState state = ModelState["OldPassword"];
            //    if (state != null)
            //    {
            //        state.Errors.Clear();
            //    }

            //    if (ModelState.IsValid)
            //    {
            //        try
            //        {
            //            WebSecurity.CreateAccount(User.Identity.Name, model.NewPassword);
            //            return RedirectToAction("Manage", new { Message = ManageMessageId.SetPasswordSuccess });
            //        }
            //        catch (Exception)
            //        {
            //            ModelState.AddModelError("", String.Format("Unable to create local account. An account with the name \"{0}\" may already exist.", User.Identity.Name));
            //        }
            //    }
            //}

            //// If we got this far, something failed, redisplay form
            //return View(model);
            #endregion

            int userID = Convert.ToInt32(User.Identity.Name);
            User user = db.Users.FirstOrDefault(a => a.UserID == userID);

            bool hasLocalAccount = user != null ? true : false;
            ViewBag.HasLocalPassword = hasLocalAccount && !string.IsNullOrEmpty(user.Pwd) ? true : false;
            bool HasLocalPassword = ViewBag.HasLocalPassword;
            ViewBag.ReturnUrl = Url.Action("ManagePassword");

            if (hasLocalAccount)
            {
                #region Local Account Section
                // Check ModelState Value or not, due to LocalPasswordModel set require old password,
                // use HasLocalPassword to skip required property for set new password module _SetPasswordPartial.
                if (ModelState.IsValid || !HasLocalPassword)
                {
                    // ChangePassword will throw an exception rather than return false in certain failure scenarios.
                    bool changePasswordSucceeded;
                    try
                    {
                        // 2016/06/17 - Bug Fix, Even incorrect old password, also can change password
                        // If empty password then assign as empty string, else error will occur while trying to encrypt Blank Password.
                        var PasswordForCompare = HasLocalPassword ? Core.Encrypt(model.OldPassword, user.PwdSalt) : string.Empty;
                        bool IsCorrectPassword = PasswordForCompare == user.Pwd ? true : false;
                        if (IsCorrectPassword)
                        {
                            #region
                            //query to insert
                            var query = "Exec Users.usp_UserLoginPwdUpdate @UserID, @Pwd, @PwdSalt, @PreviousPwd, @PreviousPwdSalt, @ModifiedByID";

                            string PasswordSalt = Core.GeneratePasswordSalt();

                            db.Database.ExecuteSqlCommand(query,
                            new SqlParameter("UserID", userID),
                            new SqlParameter("Pwd", Core.Encrypt(model.ConfirmPassword, PasswordSalt)),
                            new SqlParameter("PwdSalt", PasswordSalt),
                            new SqlParameter("PreviousPwd", string.Empty),
                            new SqlParameter("PreviousPwdSalt", string.Empty),
                            new SqlParameter("ModifiedByID", GetCurrentUserId));

                            db.SaveChanges();

                            #endregion
                            changePasswordSucceeded = true;
                        }
                        else
                            // If password mismatch result return false.
                            changePasswordSucceeded = false;

                    }
                    catch (Exception)
                    {
                        changePasswordSucceeded = false;
                    }

                    if (changePasswordSucceeded)
                    {
                        return RedirectToAction("PasswordChangeSuccess", new { Message = ManageMessageId.ChangePasswordSuccess });
                    }
                    else
                    {
                        ModelState.AddModelError("OldPassword", "Your old password does not match.");
                    }
                }
                #endregion
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        #region 2016/12/20 - Public Reset Password
        [NoCache]
        [AllowAnonymous]
        public ActionResult ResetPassword()
        {
            return View();
        }

        [NoCache]
        [HttpPost]
        [AllowAnonymous]
        [AcceptCustomButton(Name = "Reset", Value = "Password")]
        public ActionResult ResetPassword(PasswordResetModel model, string email, bool fromMailReset = false)
        {
            if (fromMailReset)
            {
                if (ViewBag.ErrMsg != null)
                    ModelState.AddModelError("errmsg", ViewBag.ErrMsg);

                return View("~/Views/Account/ResetPassword.cshtml", model);
            }
            else if (ModelState.IsValid && !fromMailReset)
            {
                UserDetail user = db.UserDetails.FirstOrDefault(x => x.User.Email == email);

                if (user != null)
                {
                    var query = "EXEC Users.usp_UserResetLinkStatus @UserID";
                    db.Database.ExecuteSqlCommand(query, new SqlParameter("@UserID", user.UserID));

                    string EncToken = GenerateResetPasswordToken(user.UserID, user.User.OrganizationID);

                    Hashtable ht = new Hashtable();
                    var url = Url.Action("MailResetPassword", "Account", new { token = EncToken }, Request.Url.Scheme);
                    ht.Add("<#UserName>", user.FirstName);
                    ht.Add("<#ResetURL>", url);
                    bool successSend = CommonServiceController.SendEmail(user.User.Email, "Forgot password? Mayflower is here to help", Core.getMailTemplate("passwordresetlink", ht));

                    if (!successSend)
                    {
                        ModelState.AddModelError("errmsg", "Fail to deliver mail to your inbox, please try again later.");
                        return View(model);
                    }
                    return RedirectToAction("ResetMailSend", "Account", new { Message = MessageId.SuccessSendResetMail });
                }
                // As Iry ARS Business Analysis requested, if mail not exist just inform user email not exist.
                ModelState.AddModelError("Email", "Account doesn't exist.");
            }
            return View(model);
        }

        [NoCache]
        [AllowAnonymous]
        public ActionResult MailResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                ViewBag.ErrMsg = "Invalid Token. Click here for a new activation email.";
                return ResetPassword(null, null, true);
            }

            string deToken = null;
            PasswordResetToken requestInfo = null;
            Cryptography.AES.TryDecrypt(token, out deToken);

            try
            {
                requestInfo = DecodeResetPasswordToken<PasswordResetToken>(deToken);
            }
            catch (Exception ex)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Error(ex, "Error on deserialize reset password token.");
            }

            if (requestInfo != null)
            {
                bool isValidRequest = requestInfo.RequestUTCDate.AddHours((double)requestInfo.TokenValidateHour) <= DateTime.UtcNow ? false : true;
                var requestUser = db.Users.FirstOrDefault(x => x.UserID == requestInfo.UserID && x.OrganizationID == requestInfo.OrganizationID);

                if (!requestUser.IsResetLinkUsed && isValidRequest)
                {
                    ViewBag.ResetPasswordToken = token;
                    return View();
                }
                else
                {
                    var uPassModel = new PasswordResetModel { Email = requestUser.Email };
                    ViewBag.ErrMsg = "Your activation email is expired. Click here for a new activation email.";
                    return ResetPassword(uPassModel, requestUser.Email, true);

                    //return RedirectToAction("Login", "Account", new { status = "link-expired" });
                }
            }
            return RedirectToAction("Login", "Account", new { status = "invalid token" });
        }

        [NoCache]
        [HttpPost]
        [AllowAnonymous]
        public ActionResult MailResetPassword(LocalPasswordModel model, string token)
        {
            if (ModelState["OldPassword"].Errors != null) { ModelState["OldPassword"].Errors.Clear(); }

            if (ModelState.IsValid)
            {
                string deToken = null;
                PasswordResetToken requestInfo = null;
                Cryptography.AES.TryDecrypt(token, out deToken);

                try
                {
                    requestInfo = DecodeResetPasswordToken<PasswordResetToken>(deToken);
                    //requestInfo = requestInfo ?? JsonConvert.DeserializeObject<PasswordResetToken>(deToken);
                }
                catch (Exception ex)
                {
                    Logger logger = LogManager.GetCurrentClassLogger();
                    logger.Error(ex, "Error on deserialize reset password token.");
                }

                if (requestInfo != null)
                {
                    bool isValidRequest = requestInfo.RequestUTCDate.AddHours((double)requestInfo.TokenValidateHour) <= DateTime.UtcNow ? false : true;
                    var requestUser = db.Users.FirstOrDefault(x => x.UserID == requestInfo.UserID);

                    if (!isValidRequest)
                    {
                        var uPassModel = new PasswordResetModel { Email = requestUser.Email };
                        ViewBag.ErrMsg = "Your activation email is expired. Click here for a new activation email.";
                        return ResetPassword(uPassModel, requestUser.Email, true);
                    }
                    else if (requestUser != null && (!requestUser.IsResetLinkUsed || (CustomPrincipal.IsLoginPasswordNotSetup && User.Identity.IsAuthenticated))
                        && isValidRequest)
                    {
                        using (var dbContextTransaction = db.Database.BeginTransaction())
                        {
                            try
                            {
                                // As discussion from Kevin, Iry, set profile activated while reset password.
                                var query = "UPDATE Users.Users SET IsProfileActivated = @IsProfileActivated WHERE UserID = @UserID";
                                db.Database.ExecuteSqlCommand(query,
                                    new SqlParameter("UserID", requestUser.UserID),
                                    new SqlParameter("IsProfileActivated", true));

                                string PasswordSalt = Core.GeneratePasswordSalt();
                                var sql = "EXEC Users.usp_UserLoginPwdUpdate @UserID, @Pwd, @PwdSalt, @PreviousPwd, @PreviousPwdSalt, @ModifiedByID";
                                db.Database.ExecuteSqlCommand(sql,
                                    new SqlParameter("UserID", requestUser.UserID),
                                        new SqlParameter("Pwd", Core.Encrypt(model.NewPassword, PasswordSalt)),
                                        new SqlParameter("PwdSalt", PasswordSalt),
                                        new SqlParameter("PreviousPwd", requestUser.Pwd),
                                        new SqlParameter("PreviousPwdSalt", requestUser.PwdSalt),
                                        new SqlParameter("ModifiedByID", GetCurrentUserId));

                                #region Send mail to notice user password changed.
                                Hashtable ht = new Hashtable();
                                ht.Add("<#UserName>", requestUser.FullName);
                                ht.Add("<#Email>", requestUser.Email);
                                ht.Add("<#Password>", model.NewPassword);
                                ht.Add("<#LoginURL>", Url.Action("Login", "Account", new { @ref = "mail" }, Request.Url.Scheme));
                                ht.Add("<#DateChange>", DateTime.Now.ToString("dd MMM yyyy hh:mm tt"));

                                CommonServiceController.SendEmail(requestUser.Email, "Mayflower - Password Changed Notice",
                                    Core.getMailTemplate("resetpasswordnotice", ht));
                                #endregion

                                dbContextTransaction.Commit();

                                if (CustomPrincipal.IsLoginPasswordNotSetup)
                                {
                                    Mayflower.General.LoginClass login = new LoginClass(requestUser, db);
                                    Response.SetAuthCookie(login.User.UserID.ToString(), false, login.UserData);
                                }
                            }
                            catch (Exception ex)
                            {
                                dbContextTransaction.Rollback();
                                Logger logger = LogManager.GetCurrentClassLogger();
                                logger.Error(ex);

                                ModelState.AddModelError("errmsg", "Update password failed, please try again later.");
                                return View(model);
                            }
                        }
                    }
                }
                else
                {
                    var uPassModel = new PasswordResetModel();
                    ViewBag.ErrMsg = "User not founds.";
                    return ResetPassword(uPassModel, null, true);
                }

                return RedirectToAction("ResetPasswordSuccess", "Account");
            }
            return View(model);
        }

        [AllowAnonymous]
        public ActionResult ResetMailSend()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult ResetPasswordSuccess()
        {
            return View();
        }

        /// <summary>
        /// Generate password token for change password validate.
        /// </summary>
        /// <param name="userid">User ID.</param>
        /// <param name="orgid">Organization ID.</param>
        /// <param name="validateHour">Token validate for specific hour.</param>
        /// <returns></returns>
        private string GenerateResetPasswordToken(int userid, int orgid, int validateHour = -1)
        {
            int TokenValidateHour = 0;

            if (validateHour == -1)
            {
                int.TryParse(Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("TokenValidateHour"), out TokenValidateHour);
            }

            var serializeJson = JsonConvert.SerializeObject(new { UserID = userid, OrganizationID = orgid, RequestUTCDate = DateTime.UtcNow, TokenValidateHour = TokenValidateHour });
            return Cryptography.AES.Encrypt(serializeJson);
        }

        private T DecodeResetPasswordToken<T>(string encrypedToken)
        {
            string dectoken = encrypedToken;
            return dectoken != "" ? JsonConvert.DeserializeObject<T>(dectoken) : default(T);
        }
        #endregion

        [Authorize]
        public ActionResult SetupPassword()
        {
            if (CustomPrincipal.IsLoginPasswordNotSetup)
            {
                var user = db.Users.FirstOrDefault(x => x.UserID == CustomPrincipal.Id);

                ViewBag.ResetPasswordToken = GenerateResetPasswordToken(CustomPrincipal.Id, user.OrganizationID);

                return View();
            }
            else
            {
                return RedirectToAction("ManagePassword", "Account");
            }
        }

        //
        // POST: /Account/ExternalLogin

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ExternalLogin(string provider, string returnUrl)
        {
            return new ChallengeResult(provider, Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl }), null, true);
        }

        //
        // GET: /Account/ExternalLoginCallback

        [AllowAnonymous]
        public async Task<ActionResult> ExternalLoginCallback(string returnUrl)
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync();
            if (loginInfo == null)
            {
                return RedirectToAction("Login");
            }
            else if (loginInfo.Email == null)
            {
                // Provider email required for associate
                return RedirectToAction("Login", "Account", new { loginerror = $"Email access required for associate {loginInfo.Login.LoginProvider} account." });
            }

            MemberServiceController.CheckUserAccount userAccount = new MemberServiceController.CheckUserAccount(loginInfo.Email, "MEM",
                loginInfo.Login.LoginProvider, loginInfo.Login.ProviderKey, db);

            if (!User.Identity.IsAuthenticated && userAccount.AvailableProviderAccount)
            {
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.LoginProvider = loginInfo.Login.LoginProvider;
                return View("~/Views/Account/_ExternalLogin/ExternalLoginConfirmation.cshtml", new ExternalLoginConfirmationViewModel { Email = loginInfo.Email });
            }
            else if (User.Identity.IsAuthenticated && userAccount.AvailableProviderAccount)
            {
                return await ExternalLoginConfirmation(new ExternalLoginConfirmationViewModel { Email = loginInfo.Email }, returnUrl);
            }
            else if (User.Identity.IsAuthenticated && !userAccount.AvailableProviderAccount)
            {
                ViewBag.PromptMsg = $"{loginInfo.Login.LoginProvider} account current using by another account.";
                return View("~/Views/Member/ManageProfile.cshtml");
            }

            var result = userAccount.TryExternalSignIn();
            switch (result)
            {
                case MemberServiceController.CheckUserAccount.SignInStatus.Success:
                    UpdateUserNameFromProvider(userAccount.User, loginInfo, db, true);
                    MemberServiceController.UpdateLoginDate(userAccount.User, db, true);
                    db.SaveChanges();

                    Mayflower.General.LoginClass login = new LoginClass(userAccount.User, db);

                    Response.SetAuthCookie(login.User.UserID.ToString(), false, login.UserData);
                    return RedirectToLocal(returnUrl);

                case MemberServiceController.CheckUserAccount.SignInStatus.LockedOut:
                    return View("Lockout");

                case MemberServiceController.CheckUserAccount.SignInStatus.RequiresVerification:
                    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = false });

                case MemberServiceController.CheckUserAccount.SignInStatus.Failure:
                default:
                    // If the user does not have an account, then prompt the user to create an account
                    ViewBag.ReturnUrl = returnUrl;
                    ViewBag.LoginProvider = loginInfo.Login.LoginProvider;
                    return View("~/Views/Account/_ExternalLogin/ExternalLoginConfirmation.cshtml", new ExternalLoginConfirmationViewModel { Email = loginInfo.Email });
            }
        }

        //
        // POST: /Account/ExternalLoginConfirmation
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model, string returnUrl)
        {
            // Get the information about the user from the external login provider
            var info = await AuthenticationManager.GetExternalLoginInfoAsync();
            if (info == null && !User.Identity.IsAuthenticated)
            {
                return View("~/Views/Account/_ExternalLogin/ExternalLoginFailure.cshtml");
            }

            MemberServiceController.CheckUserAccount userAccount = new MemberServiceController.CheckUserAccount(model.Email, "MEM",
                info.Login.LoginProvider, info.Login.ProviderKey, db);

            if (User.Identity.IsAuthenticated && !userAccount.AvailableProviderAccount)
            {
                return RedirectToAction("Index", "Home");
            }

            if (ModelState.IsValid)
            {
                if (userAccount.AvailableProviderAccount)
                {
                    int insertedUserId = User.Identity.IsAuthenticated ? GetCurrentUserId : -99;

                    SqlCommand command = new SqlCommand();
                    Exception exThrow = null;

                    try
                    {
                        if (userAccount.AvailableEmail && !User.Identity.IsAuthenticated)
                        {
                            var regModel = new MemberRegisterModels { Email = model.Email, ActivationCode = model.ReferralCode };

                            if (info.ExternalIdentity.Name != null)
                            {
                                var splitName = info.ExternalIdentity.Name.Split(' ').ToList();

                                if (splitName.Count > 1)
                                {
                                    regModel.FirstName = splitName[splitName.Count - 1];

                                    splitName.RemoveAt(splitName.Count - 1);
                                    regModel.LastName = string.Join(" ", splitName);
                                }
                            }

                            regModel.PassportName = info.ExternalIdentity.Name?.Trim() ?? "";
                            insertedUserId = MemberServiceController.InsertSimpleMember(regModel, command);
                        }

                        // Insert provider login
                        MemberServiceController.InsertProviderLogin(insertedUserId, info.Login.LoginProvider, info.Login.ProviderKey, info.Email, command);
                    }
                    catch (Exception ex)
                    {
                        // Any error rollback insertion.
                        if (command?.Transaction != null)
                            command.Transaction.Rollback();

                        exThrow = ex;
                        logger.Error(ex, $"Error on provider login - {DateTime.Now.ToString("ddMMyyyy_hhmm")}"
                            + Environment.NewLine + Environment.NewLine + $"Email used to register {model.Email}");
                    }

                    if (command?.Transaction != null)
                    {
                        command?.Transaction?.Commit();

                        if (User.Identity.IsAuthenticated)
                        {
                            return RedirectToAction("manageprofile", "member", new { res = $"{info.Login.LoginProvider.ToLower()}-auth-suc" });
                        }
                        else
                        {
                            db = db.DisposeAndRefresh(); // update instance get inserted user.
                            userAccount.RefreshUserInfo(db);

                            UpdateUserNameFromProvider(userAccount.User, info, db, true);
                            SqlCommand newCommand = new SqlCommand();
                            MemberServiceController.UpdateSimpleMemberForPromoCode(userAccount.UserDetail, newCommand);
                            newCommand?.Transaction?.Commit();

                            db = db.DisposeAndRefresh(); // update instance get inserted user.
                            userAccount.RefreshUserInfo(db);
                            //db = new MayFlower(); 
                            //userAccount = new MemberServiceController.CheckUserAccount(model.Email, "MEM",
                            //info.Login.LoginProvider, info.Login.ProviderKey, db);

                            Mayflower.General.LoginClass login = new LoginClass(userAccount.User, db);
                            Response.SetAuthCookie(login.User.UserID.ToString(), false, login.UserData);
                            return RedirectToLocal(returnUrl);
                        }
                    }
                    else
                    {
                        logger.Error($"Unable to commit transaction for {info.Login.LoginProvider} Provider Login - {DateTime.Now.ToString("ddMMyyyy_hhmm")}"
                            + Environment.NewLine + Environment.NewLine + $"Email used to register {model.Email}");
                    }
                }
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        //
        // GET: /Account/ExternalLoginFailure

        [AllowAnonymous]
        public ActionResult ExternalLoginFailure()
        {
            return View();
        }

        public ActionResult _ListPartial(bool hasPassed, string sortOrder, string currentFilter, string searchString, int? page, StatusMessage? message, string Role = null)
        {
            #region Action Return Message
            if (message == StatusMessage.success)
            { ViewBag.StatusMessage = "Booking approved."; }
            else if (message == StatusMessage.error)
            { ViewBag.StatusMessage = MvcHtmlString.Create("Unexpected error, please contact our us for help."); }
            else
            { ViewBag.StatusMessage = null; }
            #endregion

            User user = GetCurrentUserInfo;;
            Role = user.UserTypeCode;

            #region Model Initialize
            IQueryable<TripHistoryViewModel> model = null;

            if (Role == "System Admin")
            {
                model = GetBookAll(db);
            }
            else if (Role == "Company Admin" || Role == "AGT")
            {
                // hide this if publish
                //model = GetSubAgentBookByOrganizationID(GetCurrentUserInfo.OrganizationID);

                //unhide these if publish
                if (user.IsSubAgent == true)
                {
                    model = GetSubAgentBookByOrganizationID(user.OrganizationID);
                }
                else
                {
                    model = GetBookByOrganizationID(user.OrganizationID);
                }
            }
            //else if (Role == "Traveller Approver")
            //{
            //    model = GetBookByOrganizationID(GetCurrentUserInfo.OrganizationID)
            //        .Where(x => x.PendingApprovalID.Any(u => u.Equals(GetCurrentUserId)));
            //}
            //else if (User.IsInRole("Traveller Assistant"))
            //{

            //}
            else
            {
                model = GetBookByUserID(GetCurrentUserId);
            }
            #endregion

            #region Search Keyword
            if (searchString != null)
            { page = 1; }
            else
            { searchString = currentFilter; }

            if (Request.IsAjaxRequest())
            {
                Session["BookingListSearchString"] = searchString ??
                    (Session["BookingListSearchString"] != null ? Session["BookingListSearchString"].ToString() : null);
                searchString = (Session["BookingListSearchString"] != null ? Session["BookingListSearchString"].ToString() : null);
                ViewBag.CurrentFilter = searchString ?? Session["BookingListSearchString"];
            }
            else { ViewBag.CurrentFilter = searchString; Session["BookingListSearchString"] = searchString; }
            if (!String.IsNullOrEmpty(searchString))
            {
                model = model.Where(s => s.BookedUserName.Contains(searchString) || s.BookedUserGroupName.Contains(searchString)
                                       || s.BookingNo.Contains(searchString) || s.BookingStatus.BookingStatusDesc.Contains(searchString));
            }
            #endregion

            #region Filter by passed travel date & Booking Status
            if (hasPassed)
            {
                model = model.Where(x => DateTime.Now > x.To &&
                    ((x.Type == Alphareds.Module.Common.Enumeration.ProductType.Flight.ToString() && (x.BookingStatus.BookingCode.Equals("CON") || x.BookingStatus.BookingCode.Equals("QPL") || x.BookingStatus.BookingCode.Equals("TKI") ||
                    x.BookingStatus.BookingCode.Equals("RHI")
                    )) ||
                    (x.Type == Alphareds.Module.Common.Enumeration.ProductType.Hotel.ToString() && (x.BookingStatus.BookingCode.Equals("CON") || x.BookingStatus.BookingCode.Equals("RHI")))
                    || x.BookingStatus.PNRBookStatus.Equals("RHI") ||
                    (x.Type == Alphareds.Module.Common.Enumeration.ProductType.TourPackage.ToString() && (x.BookingStatus.BookingCode.Equals("CON") || x.BookingStatus.BookingCode.Equals("DPT"))) ||
                    (x.Type == Alphareds.Module.Common.Enumeration.ProductType.Event.ToString() && x.BookingStatus.BookingCode.Equals("CON")) ||
                    (x.Type == Alphareds.Module.Common.Enumeration.ProductType.CarRental.ToString() && x.BookingStatus.BookingCode.Equals("CON"))
                    ));
            }
            else
            {
                model = model.Where(x => DateTime.Now <= x.To &&
                    ((x.Type == Alphareds.Module.Common.Enumeration.ProductType.Flight.ToString() && (x.BookingStatus.BookingCode.Equals("CON") || x.BookingStatus.BookingCode.Equals("QPL") || x.BookingStatus.BookingCode.Equals("TKI") ||
                    x.BookingStatus.BookingCode.Equals("RHI") || x.BookingStatus.BookingCode.Equals("HTP")
                    )) ||
                    (x.Type == Alphareds.Module.Common.Enumeration.ProductType.Hotel.ToString() && ((x.BookingStatus.BookingCode.Equals("CON") || x.BookingStatus.BookingCode.Equals("RHI")) || x.BookingStatus.PNRBookStatus.Equals("RHI"))) ||
                    (x.Type == Alphareds.Module.Common.Enumeration.ProductType.TourPackage.ToString() && (x.BookingStatus.BookingCode.Equals("CON") || x.BookingStatus.BookingCode.Equals("DPT"))) ||
                    (x.Type == Alphareds.Module.Common.Enumeration.ProductType.Event.ToString() && x.BookingStatus.BookingCode.Equals("CON")) ||
                    (x.Type == Alphareds.Module.Common.Enumeration.ProductType.CarRental.ToString() && x.BookingStatus.BookingCode.Equals("CON"))
                    ));
            }
            #endregion

            #region Sort Order
            Session["BookingListSortOrder"] = sortOrder ??
                (Session["BookingListSortOrder"] != null ? Session["BookingListSortOrder"].ToString() : null);
            sortOrder = (Session["BookingListSortOrder"] != null ? Session["BookingListSortOrder"].ToString() : null);
            ViewBag.CurrentSort = sortOrder ?? Session["BookingListSortOrder"];
            switch (sortOrder)
            {
                case "groupname":
                    model = model.OrderBy(s => s.BookedUserGroupName);
                    break;
                case "status_asc":
                    model = model.OrderBy(s => s.BookingStatus.BookingStatusDesc);
                    break;
                case "status_desc":
                    model = model.OrderByDescending(s => s.BookingStatus.BookingStatusDesc);
                    break;
                //case "date_asc":
                //    model = model.OrderBy(s => s.BookedDepartureTime);
                //    break;
                //case "date_desc":
                //    model = model.OrderByDescending(s => s.BookedDepartureTime);
                //    break;
                //case "flight_asc":
                //    model = model.OrderBy(s => s.FlightType).ThenByDescending(t => t.BookedDepartureTime);
                //    break;
                //case "flight_desc":
                //    model = model.OrderByDescending(s => s.FlightType).ThenByDescending(t => t.BookedDepartureTime);
                //    break;
                case "user":
                    model = model.OrderBy(s => s.BookedUserName);
                    break;
                case "bookno":
                    model = model.OrderBy(s => s.BookingNo);
                    break;
                case "status":
                    model = model.OrderBy(s => s.BookingStatus.BookingCode).ThenBy(x => x.CreatedDate);
                    break;
                case "bookdate_asc":
                    model = model.OrderBy(s => s.CreatedDate).ThenBy(x => x.BookingStatus);
                    break;
                case "bookdate_desc":
                    model = model.OrderByDescending(s => s.CreatedDate).ThenBy(x => x.BookingStatus);
                    break;
                //case "departdate":
                //    model = model.OrderByDescending(s => s.BookedDepartureTime);//.ThenBy(x => x.BookingStatus);
                //    break;
                default:
                    model = model.OrderBy(s => s.From);//.ThenBy(x => x.BookingStatus);
                    break;
            }
            #endregion

            // Use IPagedModel to fix performance issues.
            int pageNumber = (page ?? 1);
            int pageSize = 10;

            var pagedModel = model.ToPagedList(pageNumber, pageSize);
            var bookingNoList = pagedModel.Select(x => x.BookingNo).Distinct();
            var _superPNRList = db.SuperPNROrders.Where(s => bookingNoList.Any(x => s.OrderNo.StartsWith(x))).AsEnumerable();
            pagedModel.ForEach(x =>
            {
                var _superPNR = _superPNRList.FirstOrDefault(s => s.OrderNo.StartsWith(x.BookingNo));
                if (x.BookingStatus.BookingCode != "DPT")
                {
                    x.BookingStatus.BookingCode = _superPNR?.BookingStatusCode ?? x.BookingStatus.BookingCode;
                    x.BookingStatus.BookingStatusDesc = db.BookingStatus.FirstOrDefault(s => s.BookingStatusCode == _superPNR.BookingStatusCode)?.BookingStatus
                    ?? x.BookingStatus.BookingStatusDesc;
                }
            });

            /* // For group SuperPNR No booking type into one row
            pagedModel = pagedModel.GroupBy(x => x.BookingNo)
                .Select(x =>
                {
                    var _firstRec = x.First();
                    var _rhiRec = x.FirstOrDefault(a => a.BookingStatus.BookingCode == "RHI")?.BookingStatus;
                    var _item = new TripHistoryViewModel
                    {
                        BookingNo = x.Key,
                        BookedUserName = _firstRec.BookedUserName,
                        BookedUserID = _firstRec.BookedUserID,
                        BookedOrganizationID = _firstRec.BookedOrganizationID,
                        BookedUserGroupName = _firstRec.BookedUserGroupName,
                        BookingStatus = _rhiRec != null ? _rhiRec : _firstRec.BookingStatus,
                        CreatedDate = _firstRec.CreatedDate,
                        Description = string.Join(", ", x.Select(s => s.Description)),
                        From = _firstRec.From,
                        To = _firstRec.To,
                        Name = _firstRec.Name,
                        Type = string.Join(", ", x.Select(s => s.Type.ToString()))
                    };

                    return _item;
                }).ToPagedList(pageNumber, pageSize);
            */

            return PartialView("_ListPartial", pagedModel);
        }

        public ActionResult UpcomingTrips(string sortOrder, string currentFilter, string searchString, int? page, StatusMessage? message)
        {
            //ViewBag.BookStatusList = new SelectList(db.BookingStatus, "BookingStatusCode", "BookingStatus");
            return Request.IsAjaxRequest()
                ? (ActionResult)this._ListPartial(false, sortOrder, currentFilter, searchString, page, message)
                : View();
            //return Request.IsAjaxRequest() ? View() : (ViewResult)PartialView("_ListPartial");
        }

        public ActionResult TravelHistory(string sortOrder, string currentFilter, string searchString, int? page, StatusMessage? message)
        {
            //ViewBag.BookStatusList = new SelectList(db.BookingStatus, "BookingStatusCode", "BookingStatus");
            return Request.IsAjaxRequest()
                ? (ActionResult)this._ListPartial(true, sortOrder, currentFilter, searchString, page, message)
                : View();
            //return Request.IsAjaxRequest() ? View() : (ViewResult)PartialView("_ListPartial");
        }

        public ActionResult SavedSearch()
        {
            return Request.IsAjaxRequest()
                ? (ActionResult)this._ListSavedSearch()
                : View();
        }

        public ActionResult _ListSavedSearch(int page = 1, string prdType = "FLT")
        {
            IQueryable<SavedSearchModel> model = null;
            int pageSize = 10;
            int.TryParse(Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("PageSize"), out pageSize);

            try
            {
                model = GetAllSavedSearch();
                if (prdType == "FLT")
                {
                    model = model.Where(x => x.PrdType == ProductTypes.Flight);
                }
                else if (prdType == "HTL")
                {
                    model = model.Where(x => x.PrdType == ProductTypes.Hotel);
                }
                return View(model.ToPagedList(page, pageSize));
            }
            catch (Exception ex)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Debug(ex.Message);
                return View(model.ToPagedList(page, pageSize));
            }
        }

        public IQueryable<SavedSearchModel> GetAllSavedSearch()
        {
            int userId = GetCurrentUserId;
            int flightAdvancedDay = 2;
            int.TryParse(Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("DayAdvance"), out flightAdvancedDay);
            var savedSearch = db.SavedSearches.Where(x => x.UserID == userId && x.IsActive == true);
            Func<SavedSearch, IEnumerable<FlightSearch>> fltSearchExp = (x => x.FlightSearches);
            Func<SavedSearch, IEnumerable<HotelSearch>> htlSearchExp = (x => x.HotelSearches);
            var fltSearch = savedSearch.Where(x => x.Type == "FLT").SelectMany(fltSearchExp).Where(x => x.DepartureDate >= DateTime.Now.AddDays(flightAdvancedDay))
                            .Select(x => new SavedSearchModel
                            {
                                SavedID = x.SavedID ?? 0,
                                Description = string.Format("{0} > {1}", x.DepartStation, x.ArrivalStation),
                                TravelDateStart = x.DepartureDate ?? DateTime.MinValue,
                                TravelDateEnd = x.ReturnDate ?? DateTime.MinValue,
                                PrdType = ProductTypes.Flight,
                                TotalSavedPrice = x.TotalFlightPrice ?? 0,
                                FlightSearchDtl = new FlightSearchInfo
                                {
                                    Adult = x.Adult ?? 0,
                                    CabinClass = x.CabinType,
                                    Child = x.Child ?? 0,
                                    DirectFlight = x.DirectFlight ?? false,
                                    Infant = x.Infant ?? 0
                                }

                            });
            var htlSearchtest = savedSearch.Where(x => x.Type == "HTL").SelectMany(htlSearchExp).Where(x => x.CheckInDateTime >= DateTime.Now.AddDays(flightAdvancedDay));
            var htlSearch = savedSearch.Where(x => x.Type == "HTL").SelectMany(htlSearchExp).Where(x => x.CheckInDateTime >= DateTime.Now.AddDays(flightAdvancedDay))
                            .Select(x => new SavedSearchModel
                            {
                                SavedID = x.SavedID ?? 0,
                                Description = x.HotelSearchDetails.FirstOrDefault()?.HotelCity,
                                TravelDateStart = x.CheckInDateTime ?? DateTime.MinValue,
                                TravelDateEnd = x.CheckOutDateTime ?? DateTime.MinValue,
                                PrdType = ProductTypes.Hotel,
                                TotalSavedPrice = x.HotelSearchDetails.Sum(y => y.HotelPrice) ?? 0,
                                HotelSearchInfo = new HotelSearchInfo
                                {
                                    Adult = x.Adult ?? 0,
                                    Child = x.Child ?? 0,
                                    NoOfRoom = x.HotelSearchDetails.FirstOrDefault()?.NoOfRoom ?? 0,
                                    HotelName = x.HotelSearchDetails.FirstOrDefault()?.HotelName,
                                },
                            });

            fltSearch = fltSearch.Concat(htlSearch);
            return fltSearch.AsQueryable();
        }

        public ActionResult RemoveSavedResult(string savedID)
        {
            int saveID = 0;
            int.TryParse(savedID, out saveID);

            try
            {
                //If didn't get saveID, then return to home page
                if (saveID == 0)
                {
                    return RedirectToAction("SavedSearch");
                }

                var savedSearch = db.SavedSearches.FirstOrDefault(x => x.SavedID == saveID);
                if (savedSearch == null)
                {
                    return RedirectToAction("SavedSearch");
                }
                else
                {
                    if (savedSearch.Type == "FLT" || savedSearch.Type == "HTL")
                    {
                        var oldSession = db.SavedSearches.FirstOrDefault(a => a.SavedID == saveID);
                        oldSession.IsActive = false;
                        db.SaveChanges();
                        db.Dispose();
                    }
                }
                return RedirectToAction("SavedSearch");
            }
            catch (Exception ex)
            {
                Logger log = LogManager.GetCurrentClassLogger();
                log.Debug(ex.Message);

                return RedirectToAction("SavedSearch");
            }
        }

        [HttpPost]
        public ActionResult SavedSearch(string savedID, string skipCheckPrice)
        {
            int saveID = 0;
            int skipCheck = 0;
            int.TryParse(savedID, out saveID);
            int.TryParse(skipCheckPrice, out skipCheck);

            try
            {
                //If didn't get saveID, then return to home page
                if (saveID == 0)
                {
                    return RedirectToAction("SavedSearch");
                }

                var savedSearch = db.SavedSearches.FirstOrDefault(x => x.SavedID == saveID);
                CheckoutProduct checkoutProduct = (CheckoutProduct)Alphareds.Module.Common.Core.GetSession(Alphareds.Module.Common.Enumeration.SessionName.CheckoutProduct, tripid) ?? new CheckoutProduct();

                if (savedSearch == null)
                {
                    return RedirectToAction("SavedSearch");
                }
                else
                {
                    decimal ttlPriceSaved = 0;
                    if (savedSearch.Type == "FLT")
                    {
                        //Match Flight Here
                        var fltSearch = savedSearch.FlightSearches.FirstOrDefault();
                        var fltDtl = fltSearch.FlightSearchDetails;
                        ttlPriceSaved = fltSearch.TotalFlightPrice ?? 0;
                        SearchFlightResultViewModel searchModel = new SearchFlightResultViewModel
                        {
                            Adults = fltSearch.Adult ?? 0,
                            ArrivalStation = fltSearch.ArrivalStation,
                            BeginDate = fltSearch.DepartureDate,
                            CabinClass = fltSearch.CabinType,
                            Childrens = fltSearch.Child ?? 0,
                            DepartureStation = fltSearch.DepartStation,
                            DirectFlight = fltSearch.DirectFlight ?? false,
                            EndDate = fltSearch.ReturnDate,
                            Infants = fltSearch.Infant ?? 0,
                            TripType = fltSearch.TripTypeID == 1 ? "OneWay" : "Return" //TripTypeID == 1 is OneWay, 2 is Return
                        };
                        List<FlightSegmentModels> segModel = new List<FlightSegmentModels>();
                        int index = 1;
                        foreach (var dtl in fltDtl)
                        {
                            segModel.Add(new FlightSegmentModels
                            {
                                airline_Code = dtl.AirlineCode,
                                Class = dtl.ResBookDesignCode,
                                departure_Date = dtl.DepartureDateTime ?? DateTime.Now,
                                des = dtl.ArrivalStation,
                                flight_No = dtl.FlightNumber,
                                index = index++,
                                isOutBoundSeg = dtl.SegmentOrder.Contains("O"),
                                ori = dtl.DepartureStation
                            });
                        }

                        if (skipCheck == 1)
                        {
                            var flightRes = Alphareds.Module.ServiceCall.CompareToolServiceCall.RequestFlight(searchModel);

                            if (flightRes.Errors == null || flightRes.FlightData.Length > 0)
                            {
                                var flightList = flightRes.FlightData.Where(x => x.ServiceSource == Alphareds.Module.Common.UtilitiesService.GetSupplier(fltSearch.SupplierCode)).ToList();
                                if (!Alphareds.Module.Common.Core.IsEnableB2B)
                                {
                                    flightList = Alphareds.Module.FlightSearchController.FlightSearchServiceController.bindGSTandServiceFeeToFlightResults(flightList, searchModel.isDomesticFlight, searchModel.CabinClass);
                                }

                                var matchedFlight = new Alphareds.Module.FlightSearchController.FlightSearchServiceController().MatchFlight(flightRes.FlightData, segModel.ToArray());
                                if (matchedFlight != null && matchedFlight.Length > 0)
                                {
                                    var selectedFlight = matchedFlight.FirstOrDefault();
                                    var ttlNewPrice = selectedFlight.pricedItineryModel.PricingInfo.TotalAfterTax;

                                    //Throw a message to front end indicate price is higher
                                    if (ttlPriceSaved > ttlNewPrice && skipCheck == 1)
                                    {
                                        ProductFlight prdFlight = Alphareds.Module.FlightSearchController.FlightSearchServiceController.GenerateFlightProduct(
                                            selectedFlight.pricedItineryModel, searchModel, selectedFlight.ServiceSource, General.Utilities.GetClientIP);

                                        checkoutProduct.IsRegister = false; // reset last option
                                        checkoutProduct.ImFlying = false;
                                        checkoutProduct.RequireInsurance = false;
                                        checkoutProduct.RemoveProduct(ProductTypes.Flight);
                                        checkoutProduct.InsertProduct(prdFlight);

                                        Alphareds.Module.Common.Core.SetSession(Alphareds.Module.Common.Enumeration.SessionName.CheckoutProduct, tripid, checkoutProduct);
                                        return Json(new { isLowerPrice = true, isMatched = true, currentPrice = ttlNewPrice, savedPrice = ttlPriceSaved }, JsonRequestBehavior.AllowGet);
                                    }
                                    else if (ttlPriceSaved < ttlNewPrice && skipCheck == 1)
                                    {
                                        ProductFlight prdFlight = Alphareds.Module.FlightSearchController.FlightSearchServiceController.GenerateFlightProduct(
                                            selectedFlight.pricedItineryModel, searchModel, selectedFlight.ServiceSource, General.Utilities.GetClientIP);

                                        checkoutProduct.IsRegister = false; // reset last option
                                        checkoutProduct.ImFlying = false;
                                        checkoutProduct.RequireInsurance = false;
                                        checkoutProduct.RemoveProduct(ProductTypes.Flight);
                                        checkoutProduct.InsertProduct(prdFlight);

                                        Alphareds.Module.Common.Core.SetSession(Alphareds.Module.Common.Enumeration.SessionName.CheckoutProduct, tripid, checkoutProduct);
                                        return Json(new { isHigherPrice = true, isMatched = true, currentPrice = ttlNewPrice, savedPrice = ttlPriceSaved }, JsonRequestBehavior.AllowGet);
                                    }
                                    else if (ttlPriceSaved == ttlNewPrice && skipCheck == 1)
                                    {
                                        ProductFlight prdFlight = Alphareds.Module.FlightSearchController.FlightSearchServiceController.GenerateFlightProduct(
                                            selectedFlight.pricedItineryModel, searchModel, selectedFlight.ServiceSource, General.Utilities.GetClientIP);

                                        checkoutProduct.IsRegister = false; // reset last option
                                        checkoutProduct.ImFlying = false;
                                        checkoutProduct.RequireInsurance = false;
                                        checkoutProduct.RemoveProduct(ProductTypes.Flight);
                                        checkoutProduct.InsertProduct(prdFlight);

                                        Alphareds.Module.Common.Core.SetSession(Alphareds.Module.Common.Enumeration.SessionName.CheckoutProduct, tripid, checkoutProduct);
                                        return Json(new { isEqual = true }, JsonRequestBehavior.AllowGet);
                                    }
                                }
                                else
                                {
                                    return Json(new { isEmptyFlight = true }, JsonRequestBehavior.AllowGet);
                                }
                            }
                            else
                            {
                                throw new Exception("Flight didn't exist");
                            }
                        }
                    }
                    else if (savedSearch.Type == "HTL")
                    {
                        //Match Hotel here and set the checkout session
                        var htlSearch = savedSearch.HotelSearches.FirstOrDefault();
                        var htlDtl = htlSearch.HotelSearchDetails;
                        var supplierCode = htlDtl.FirstOrDefault().SupplierCode;
                        ttlPriceSaved = htlSearch.HotelSearchDetails.Sum(x => x.HotelPrice) ?? 0;
                        SearchHotelModel searchModel = new SearchHotelModel
                        {
                            ArrivalDate = htlSearch.CheckInDateTime ?? DateTime.Now,
                            DepartureDate = htlSearch.CheckOutDateTime ?? DateTime.Now,
                            CurrencyCode = "MYR",
                            Destination = htlDtl.FirstOrDefault().HotelCity,
                            CustomerUserAgent = Request.UserAgent,
                            CustomerIpAddress = Request.UserHostAddress,
                            CustomerSessionId = Guid.NewGuid().ToString(),
                            NoOfAdult = htlSearch.Adult ?? 0,
                            NoOfInfant = htlSearch.Child ?? 0,
                            NoOfRoom = htlDtl.FirstOrDefault()?.NoOfRoom ?? 0,
                            SupplierIncluded = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier()
                            {
                                Expedia = supplierCode.Contains("EAN") ? true : false,
                                JacTravel = supplierCode.Contains("JAC") ? true : false,
                                Tourplan = supplierCode.Contains("TP") ? true : false,
                                HotelBeds = supplierCode.Contains("HB") ? true : false,
                                EANRapid = supplierCode.Contains("RAP"),
                            }
                        };
                        SearchRoomModel searchRoomModel = new SearchRoomModel
                        {
                            ArrivalDate = searchModel.ArrivalDate,
                            DepartureDate = searchModel.DepartureDate,
                            CurrencyCode = searchModel.CurrencyCode,
                            CustomerUserAgent = searchModel.CustomerUserAgent,
                            CustomerIpAddress = searchModel.CustomerIpAddress,
                            CustomerSessionId = searchModel.CustomerSessionId,
                            HotelID = htlDtl.FirstOrDefault().HotelID,
                        };
                        if (skipCheck == 1)
                        {
                            List<string> HotelId = new List<string>();
                            HotelId.Add(htlDtl.FirstOrDefault().HotelID);
                            searchModel.Result = Alphareds.Module.ServiceCall.ESBHotelServiceCall.GetHotelList(searchModel, HotelId);
                            searchRoomModel.Result = Alphareds.Module.ServiceCall.ESBHotelServiceCall.GetRoomAvailability(searchRoomModel, searchModel);
                            if (searchRoomModel.Result != null && searchRoomModel.Result.Errors == null && searchRoomModel.Result.HotelRoomInformationList.Length > 0)
                            {
                                var matchedHotel = searchRoomModel.Result.HotelRoomInformationList.FirstOrDefault().roomAvailabilityDetailsList.Where(x => htlDtl.Any(y => y.RoomTypeCode == x.roomTypeCode)).ToList();

                                if (matchedHotel != null && matchedHotel.Count > 0)
                                {
                                    decimal ttlNewPrice = 0;
                                    List<RoomSelectedModel> roomSelectedList = new List<RoomSelectedModel>();
                                    foreach (var room in htlDtl)
                                    {
                                        int roomqty = room.NoOfRoom ?? 0;
                                        var roomdtl = matchedHotel.FirstOrDefault(x => x.roomTypeCode == room.RoomTypeCode);
                                        if (roomdtl != null)
                                        {
                                            ttlNewPrice += Convert.ToDecimal(roomdtl.RateInfos.FirstOrDefault().chargeableRateInfo.total) * roomqty;
                                            roomSelectedList.Add(new RoomSelectedModel()
                                            {
                                                Hotel = room.HotelID,
                                                Name = room.HotelName,
                                                TypeCode = room.RoomTypeCode,
                                                Qty = roomqty,
                                                PropertyId = roomdtl.propertyId,
                                                RateCode = roomdtl.rateCode,
                                            });
                                        }
                                    }

                                    Alphareds.Module.HotelController.HotelServiceController.InitializeModel hc = new Alphareds.Module.HotelController.HotelServiceController.InitializeModel(tripid, Request.UserAgent, searchModel.CustomerIpAddress);
                                    List<SearchRoomModel> searchRoomModelList = new List<SearchRoomModel> { searchRoomModel };
                                    List<GTM_HotelProductModel> _GTM_addToCartList = new List<GTM_HotelProductModel>();
                                    List<RoomDetail> _roomDetails = hc.InitializeRoomDetailModel(roomSelectedList, searchRoomModelList, out _GTM_addToCartList);

                                    ProductPricingDetail hotelPricingDetail = new ProductPricingDetail
                                    {
                                        Sequence = 2,
                                        Currency = searchRoomModel.CurrencyCode,
                                        Items = _roomDetails.GroupBy(x => new { x.RoomTypeCode, x.RoomTypeName, x.TotalBaseRate, x.TotalTaxAndServices }).Select(x => new ProductItem
                                        {
                                            ItemDetail = x.Key.RoomTypeName,
                                            ItemQty = x.Count(),
                                            BaseRate = x.Key.TotalBaseRate,
                                            Surcharge = x.Key.TotalTaxAndServices,
                                            Supplier_TotalAmt = x.Sum(s => s.TotalBaseRate_Source) + x.Sum(s => s.TotalTaxAndServices_Source) + x.Sum(s => s.TotalGST_Source),
                                            GST = 0,
                                        }).ToList(),
                                        Discounts = new List<DiscountDetail>(),
                                    };

                                    ProductHotel prdHotel = new ProductHotel
                                    {
                                        ContactPerson = null,
                                        SearchHotelInfo = searchModel,
                                        RoomDetails = _roomDetails,
                                        RoomAvailabilityResponse = searchRoomModel.Result,
                                        SearchRoomList = searchRoomModelList,
                                        ProductSeq = 2,
                                        PricingDetail = hotelPricingDetail,
                                    };
                                    checkoutProduct.IsRegister = false; // reset last option
                                    checkoutProduct.ImFlying = false;
                                    checkoutProduct.RequireInsurance = false;
                                    checkoutProduct.RemoveProduct(ProductTypes.Hotel);
                                    checkoutProduct.InsertProduct(prdHotel);

                                    //Throw a message to front end indicate price is higher
                                    if (ttlPriceSaved > ttlNewPrice && skipCheck == 1)
                                    {
                                        Alphareds.Module.Common.Core.SetSession(Alphareds.Module.Common.Enumeration.SessionName.CheckoutProduct, tripid, checkoutProduct);
                                        return Json(new { isLowerPrice = true, isMatched = true, currentPrice = ttlNewPrice, savedPrice = ttlPriceSaved }, JsonRequestBehavior.AllowGet);
                                    }
                                    else if (ttlPriceSaved < ttlNewPrice && skipCheck == 1)
                                    {
                                        Alphareds.Module.Common.Core.SetSession(Alphareds.Module.Common.Enumeration.SessionName.CheckoutProduct, tripid, checkoutProduct);
                                        return Json(new { isHigherPrice = true, isMatched = true, currentPrice = ttlNewPrice, savedPrice = ttlPriceSaved }, JsonRequestBehavior.AllowGet);
                                    }
                                    else if (ttlPriceSaved == ttlNewPrice && skipCheck == 1)
                                    {
                                        Alphareds.Module.Common.Core.SetSession(Alphareds.Module.Common.Enumeration.SessionName.CheckoutProduct, tripid, checkoutProduct);
                                        return Json(new { isEqual = true }, JsonRequestBehavior.AllowGet);
                                    }
                                }
                                else
                                {
                                    return Json(new { isEmptyHotel = true }, JsonRequestBehavior.AllowGet);
                                }
                            }
                            else
                            {
                                throw new Exception("Hotel didn't exist");
                            }
                        }
                    }
                    else
                    {
                        return RedirectToAction("SavedSearch");
                    }
                }
                Alphareds.Module.Common.Core.SetSession(Alphareds.Module.Common.Enumeration.SessionName.CheckoutProduct, tripid, checkoutProduct);
                return RedirectToAction("GuestDetails", "Checkout", new { tripid, previousUrl = "SavedSearch" });
            }
            catch (Exception ex)
            {
                Logger log = LogManager.GetCurrentClassLogger();
                log.Debug(ex.Message);

                return RedirectToAction("SavedSearch");
            }
        }

        public IQueryable<TripHistoryViewModel> GetBookAll(MayFlower db = null)
        {
            db = db ?? new MayFlower();

            var modelFlight = from a in db.Bookings
                              select new TripHistoryViewModel()
                              {
                                  //PendingApprovalID = db.UsersApprovers.Where(x => x.UserId == a.UserID).Select(y => y.ApproverUserID).ToList(),
                                  BookedUserID = a.UserID,
                                  BookedOrganizationID = a.User.OrganizationID,
                                  BookingNo = a.SuperPNRNo,
                                  BookingStatus = new BookingStatusModels
                                  {
                                      BookingCode = a.BookingStatusCode,
                                      BookingStatusDesc = a.BookingStatu.BookingStatus,
                                      PNRBookStatus = a.SuperPNR.SuperPNROrders.FirstOrDefault().BookingStatusCode
                                  },
                                  CreatedDate = a.CreatedDate,
                                  BookedUserName = a.ReceivedFrom,
                                  From = a.FlightSegments.OrderBy(x => x.FlightSegmentID).FirstOrDefault().DepartureDateTime,
                                  To = a.FlightSegments.OrderByDescending(x => x.FlightSegmentID).FirstOrDefault().ArrivalDateTime,
                                  Description = a.Origin + " > " + a.Destination,
                                  Name = a.Paxes.Where(x => (x.IsContactPerson != null ? (bool)!x.IsContactPerson : false)).Select(y => y.GivenName + @"/" + y.Surname).FirstOrDefault(),
                                  Type = Alphareds.Module.Common.Enumeration.ProductType.Flight.ToString(),
                              };


            var modelHotel = from a in db.BookingHotels
                             select new TripHistoryViewModel()
                             {
                                 //PendingApprovalID = db.UsersApprovers.Where(x => x.UserId == a.UserID).Select(y => y.ApproverUserID).ToList(),
                                 BookedUserID = a.UserID,
                                 BookedOrganizationID = a.User.OrganizationID,
                                 BookingNo = a.SuperPNRNo,//a.SupplierCode.Equals("EAN") ? a.RoomPaxHotels.Where(x => x.BookingID == a.BookingID).Select(y => y.ItineraryNumber + y.RoomConfirmationNumber).FirstOrDefault() : a.BookingNo,
                                 BookingStatus = new BookingStatusModels
                                 {
                                     BookingCode = a.BookingStatusCode,
                                     BookingStatusDesc = a.BookingStatu.BookingStatus,
                                     PNRBookStatus = a.SuperPNR.SuperPNROrders.FirstOrDefault().BookingStatusCode
                                 },
                                 CreatedDate = a.CreatedDate,
                                 BookedUserName = a.ReceivedFrom,
                                 From = a.CheckInDateTime,
                                 To = a.CheckOutDateTime,
                                 Description = a.HotelName,
                                 Name = a.RoomPaxHotels.Where(x => (x.IsContactPerson != null ? (bool)!x.IsContactPerson : false)).Select(y => y.GivenName + @"/" + y.Surname).FirstOrDefault(),
                                 Type = Alphareds.Module.Common.Enumeration.ProductType.Hotel.ToString(),
                             };

            var modelTour = from a in db.TourPackageBookings
                            join user in db.Users on a.CreatedByID equals user.UserID into userJoin
                            join tpm in db.TourPackageMasters on a.TourPackageID equals tpm.TourPackageID into tpmJoin
                            join bks in db.BookingStatus on a.BookingStatusCode equals bks.BookingStatusCode
                            from user in userJoin.DefaultIfEmpty()
                            from tpm in tpmJoin.DefaultIfEmpty()
                            select new TripHistoryViewModel()
                            {
                                BookedUserID = a.CreatedByID,
                                BookedOrganizationID = user != null ? user.OrganizationID : 0,
                                BookingNo = a.SuperPNRNo,
                                BookingStatus = new BookingStatusModels
                                {
                                    BookingCode = a.BookingStatusCode,
                                    BookingStatusDesc = bks.BookingStatus,
                                    PNRBookStatus = a.SuperPNR.SuperPNROrders.FirstOrDefault().BookingStatusCode,
                                },
                                CreatedDate = a.CreatedDate,
                                BookedUserName = a.PassengerEmail,
                                From = a.TravelDateFrom,
                                To = a.TravelDateTo,
                                Description = tpm != null ? tpm.TourPackageName : null,
                                Name = a.GivenName + " " + a.SurName,
                                Type = Alphareds.Module.Common.Enumeration.ProductType.TourPackage.ToString(),
                            };

            var modelEventBundle = from a in db.EventBookings
                                   where a.SuperPNR.Bookings.Count == 0 && a.SuperPNR.BookingHotels.Count == 0 && a.SuperPNR.TourPackageBookings.Count == 0
                                   group a by a.SuperPNRNo into evGrp
                                   let eventBundle = evGrp.FirstOrDefault()
                                   //x.SuperPNR.BookingHotels.Count == 0 && x.SuperPNR.TourPackageBookings.Count == 0).GroupBy(x => x.SuperPNRNo)
                                   //let eventBundle = a.FirstOrDefault(x => x.EventProduct.EventDetail.EventMaster.EventTypeCode == "CT")
                                   join user in db.Users on eventBundle.CreatedByID equals user.UserID into userJoin
                                   join bks in db.BookingStatus on eventBundle.BookingStatusCode equals bks.BookingStatusCode
                                   join bkc in db.BookingContacts on eventBundle.SuperPNRID equals bkc.SuperPNRID into bkcJoin
                                   join evB in db.EventBundles on eventBundle.EventProduct.EventDetail.EventID equals evB.EventID into evBJoin
                                   from user in userJoin.DefaultIfEmpty()
                                   from bkc in bkcJoin.DefaultIfEmpty()
                                   from evB in evBJoin.DefaultIfEmpty()
                                   select new TripHistoryViewModel()
                                   {
                                       BookedUserID = eventBundle.CreatedByID,
                                       BookedOrganizationID = user != null ? user.OrganizationID : 0,
                                       BookingNo = eventBundle.SuperPNRNo,
                                       BookingStatus = new BookingStatusModels
                                       {
                                           BookingCode = eventBundle.BookingStatusCode,
                                           BookingStatusDesc = bks.BookingStatus,
                                           PNRBookStatus = eventBundle.SuperPNR.SuperPNROrders.FirstOrDefault().BookingStatusCode,
                                       },
                                       CreatedDate = eventBundle.CreatedDate,
                                       BookedUserName = bkc != null ? bkc.Email : "-",
                                       From = eventBundle.EventDate,
                                       To = eventBundle.EventDate,
                                       Description = evB != null ? evB.Title : null,
                                       Name = bkc != null ? bkc.GivenName + " " + bkc.Surname : null,
                                       Type = Alphareds.Module.Common.Enumeration.ProductType.Event.ToString(),
                                   };

            var modelCar = from a in db.CarRentalBookings
                           join user in db.Users on a.CreatedByID equals user.UserID into userJoin
                           join bks in db.BookingStatus on a.BookingStatusCode equals bks.BookingStatusCode
                           from user in userJoin.DefaultIfEmpty()
                           select new TripHistoryViewModel()
                           {
                               BookedUserID = a.UserID,
                               BookedOrganizationID = user.OrganizationID,
                               BookingNo = a.SuperPNRNo,
                               BookingStatus = new BookingStatusModels
                               {
                                   BookingCode = a.BookingStatusCode,
                                   BookingStatusDesc = bks.BookingStatus,
                                   PNRBookStatus = a.SuperPNR.SuperPNROrders.FirstOrDefault().BookingStatusCode
                               },
                               CreatedDate = a.CreatedDate,
                               BookedUserName = a.ReceivedFrom,
                               From = a.ScheduledPickUpDateTime,
                               To = a.ScheduledReturnDateTime,
                               Description = a.VehicleName,
                               Name = a.SuperPNR.BookingContact.GivenName + " " + a.SuperPNR.BookingContact.Surname,
                               Type = Alphareds.Module.Common.Enumeration.ProductType.CarRental.ToString(),
                           };

            var model = modelFlight.Concat(modelHotel);
            model = model.Concat(modelTour);
            model = model.Concat(modelEventBundle);
            model = model.Concat(modelCar);
            return model.AsQueryable();
        }

        public IQueryable<TripHistoryViewModel> GetBookByOrganizationID(int OrganizationID)
        {
            var model = GetBookAll().Where(x => x.BookedOrganizationID == OrganizationID);
            return model;
        }

        public IQueryable<TripHistoryViewModel> GetSubAgentBookByOrganizationID(int OrganizationID)
        {
            var subagent = db.Users.Where(x => x.OrganizationID == OrganizationID && x.IsSubAgent == true).Select(x => x.UserID).ToList();
            var model = GetBookAll().Where(x => subagent.Any(p => p == x.BookedUserID));
            return model;
        }

        public IQueryable<TripHistoryViewModel> GetBookByUserID(int UserID)
        {
            var model = GetBookAll().Where(x => x.BookedUserID == UserID);
            return model;
        }

        private User GetCurrentUserInfo
        {
            get
            {
                return db.Users.FirstOrDefault(x => x.UserID == GetCurrentUserId);
            }
        }

        #region Helpers
        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        internal class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri)
                : this(provider, redirectUri, null, false)
            {
            }

            public ChallengeResult(string provider, string redirectUri, string userId, bool isRerequest)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
                IsRerequest = isRerequest;
            }

            public string LoginProvider { get; set; }
            public string RedirectUri { get; set; }
            public string UserId { get; set; }
            public bool IsRerequest { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties { RedirectUri = RedirectUri };
                if (UserId != null)
                {
                    properties.Dictionary[XsrfKey] = UserId;
                }
                if (IsRerequest)
                {
                    properties.Dictionary["auth_type"] = "rerequest";
                }
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }

        public enum ManageMessageId
        {
            ChangePasswordSuccess,
            SetPasswordSuccess,
            RemoveLoginSuccess,
            IncorrectPassword
        }

        private static string ErrorCodeToString(MembershipCreateStatus createStatus)
        {
            // See http://go.microsoft.com/fwlink/?LinkID=177550 for
            // a full list of status codes.
            switch (createStatus)
            {
                case MembershipCreateStatus.DuplicateUserName:
                    return "User name already exists. Please enter a different user name.";

                case MembershipCreateStatus.DuplicateEmail:
                    return "A user name for that e-mail address already exists. Please enter a different e-mail address.";

                case MembershipCreateStatus.InvalidPassword:
                    return "The password provided is invalid. Please enter a valid password value.";

                case MembershipCreateStatus.InvalidEmail:
                    return "The e-mail address provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidAnswer:
                    return "The password retrieval answer provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidQuestion:
                    return "The password retrieval question provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidUserName:
                    return "The user name provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.ProviderError:
                    return "The authentication provider returned an error. Please verify your entry and try again. If the problem persists, please contact your system administrator.";

                case MembershipCreateStatus.UserRejected:
                    return "The user creation request has been canceled. Please verify your entry and try again. If the problem persists, please contact your system administrator.";

                default:
                    return "An unknown error occurred. Please verify your entry and try again. If the problem persists, please contact your system administrator.";
            }
        }

        private void UpdateUserNameFromProvider(User user, ExternalLoginInfo loginInfo, MayFlower db, bool saveExternal = false)
        {
            var userDtl = user.UserDetails.FirstOrDefault();
            if (userDtl != null && (userDtl.FirstName == "NA" && userDtl.LastName == "NA") && loginInfo.ExternalIdentity.Name != null)
            {
                user.IsProfileActivated = true;

                var splitName = loginInfo.ExternalIdentity.Name.Split(' ').ToList();

                if (splitName.Count > 1)
                {
                    userDtl.FirstName = splitName[splitName.Count - 1];
                    splitName.RemoveAt(splitName.Count - 1);
                    userDtl.LastName = string.Join(" ", splitName);
                }

                userDtl.PrimaryPhone = userDtl.PrimaryPhone == "NA" ? "" : userDtl.PrimaryPhone;
                userDtl.SecondaryPhone = userDtl.SecondaryPhone == "NA" ? "" : userDtl.SecondaryPhone;

                try
                {
                    if (!saveExternal)
                    {
                        db.SaveChanges();
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

        }
        #endregion

        /*private bool IsCompanyProfileActive(string UserID)
        {
            int userID = Convert.ToInt32(UserID);
            bool IsCompanyActive = db.Users.FirstOrDefault(x => x.UserID == userID).Organization.IsProfileActivated;

            return IsCompanyActive;
        }

        private bool IsPersonalProfileActive(string UserID)
        {
            int userID = Convert.ToInt32(UserID);
            bool IsPersonalProfileActivated = db.Users.FirstOrDefault(x => x.UserID == userID).IsProfileActivated;
            
            return IsPersonalProfileActivated;
        }*/

        private int GetCurrentUserId
        {
            get
            {
                int userid = 0;
                int.TryParse(User.Identity.Name, out userid);
                return userid;
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                db?.Dispose();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error while dispose account controller db context.");
            }

            base.Dispose(disposing);
        }
    }
}
