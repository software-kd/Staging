using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Alphareds.Module.Model.Database;
using Alphareds.Module.Model;
using PagedList;
using System.Data.SqlClient;
//using Mayflower.General;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using Mayflower.Filters;
using System.Net;
using System.IO;
using System.Dynamic;
using NLog;
using Alphareds.Module.MemberController;
using Alphareds.Module.CommonController;
using Alphareds.Module.Cryptography;
using Newtonsoft.Json;
using Alphareds.Module.Common;
using System.Configuration;
using MySql.Data.MySqlClient;

namespace Mayflower.Controllers
{
    public class MemberController : Controller
    {
        private MayFlower db = new MayFlower();
        private Logger logger = LogManager.GetCurrentClassLogger();

        public enum ManageMessageId
        {
            RegisterSuccess,
            RegisterFail
        }

        #region CheckInfoAvailability
        //JSON Check CompanyName
        [AllowAnonymous]
        public JsonResult IsCompanyNameAvailable(string CompanyName)
        {
            return Json(!(db.UserDetails.Any(x => x.CompanyName == CompanyName)), JsonRequestBehavior.AllowGet);
        }

        //JSON Check Email, current use for member only
        [AllowAnonymous]
        public JsonResult IsEmailAvailable(string Email)
        {
            MayFlower db = new MayFlower();

            try
            {
                bool isMemNotReg = !db.Users.Any(x => x.Email.ToLower() == Email.ToLower() && x.UserTypeCode == "MEM");

                return Json(isMemNotReg, JsonRequestBehavior.AllowGet);
                //return Json(!((db.Users.Where(x => x.UserTypeCode == "BOS").Any(x => x.Email == Email)) || 
                //    (db.Users.Where(x => x.UserTypeCode == "MEM").Any(x => x.Email == Email))), JsonRequestBehavior.AllowGet);
            }
            catch
            {
                return Json(false);
            }

        }

        public JsonResult IsFFAirlineExist(string AirlineCode)
        {
            return Json(!(db.UserFrequentFlyers.Any(x => x.AirlineCode == AirlineCode && x.UserID == CurrentUserID)), JsonRequestBehavior.AllowGet);
        }
        #endregion

        // GET: return view success activated
        [AllowAnonymous]
        public ActionResult SuccessValidate()
        {
            var email = Request.QueryString["Email"];
            User user = db.Users.FirstOrDefault(x => x.Email == email);

            if (user == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            if (user.IsActive)
            {
                return View("SuccessValidate", new { isActivated = "true" });
            }

            return View();
        }

        // GET: return view success activated
        [AllowAnonymous]
        public ActionResult SuccessRegister()
        {
            var success = Request.QueryString["Message"];

            if (success != null && success == "RegisterSuccess")
            {
                return View();
            }

            return View("Index");
        }

        /// <param name="token"> UserID encoding by Base64. </param>
        /// <param name="email"> User Email Address required to activate. </param>

        /// GET: /Member/ActivateByEmailLink
        [AllowAnonymous]
        public ActionResult ActivateByEmailLink(string token, string email)
        {
            int userID = -1;
            bool success = int.TryParse(General.CustomizeBaseEncoding.DeCodeBase64(token), out userID);

            if (!success)
            {
                string uidString = "-1";
                Cryptography.AES.TryDecrypt(token, out uidString);

                userID = Convert.ToInt32(uidString);
                success = userID > 0;
            }

            if (success)
            {
                User user = db.Users.FirstOrDefault(u => u.UserID == userID);

                if (user != null)
                {
                    if (user.IsProfileActivated == false && user.Email == email)
                    {
                        user.IsProfileActivated = true;
                        //user.IsActive = true;
                        db.SaveChanges();

                        //Response.SetAuthCookie(userData.UserId, false, userData);
                        GenerateVerifiedMail(user.UserDetails.FirstOrDefault()?.FirstName ?? "", email);
                        return RedirectToAction("SuccessValidate", "Member", new { Email = user.Email.ToLower() });
                    }
                }
            }

            // 2016/05/26 - Any failure return to home page.
            return RedirectToAction("Index", "Home");
        }

        /// GET: /Member/ActivateByEmailLink
        [AllowAnonymous]
        public ActionResult SimpleActivateByEmailLink(string token, string email, string aType)
        {
            int userID = -1;

            if (aType != null && aType.ToLower() == "nopass")
            {
                string idString = "-1";
                Cryptography.AES.TryDecrypt(token, out idString);
                int.TryParse(idString, out userID);
            }

            if (userID != -1 && !string.IsNullOrWhiteSpace(email))
            {
                User user = db.Users.FirstOrDefault(u => u.UserID == userID &&
                (u.Email == null || u.Email.ToLower() == email.ToLower()));

                if (user != null)
                {
                    if (!user.IsProfileActivated)
                    {
                        //user.IsProfileActivated = true;
                        //db.SaveChanges();

                        return RedirectToAction("UpdateAccount", "Member", new { token, email = user.Email.ToLower() });
                    }
                    else if (user.Pwd == "NA")
                    {
                        return RedirectToAction("UpdateAccount", "Member", new { token, email = user.Email.ToLower() });
                    }
                }
            }

            // 2016/05/26 - Any failure return to home page.
            return RedirectToAction("Index", "Home");
        }

        //
        // GET: /Member/Register
        public ActionResult Register(ManageMessageId? message)
        {
            return RedirectToAction("SimpleRegister", "Member");
            StringBuilder builderSuccess = new StringBuilder();
            builderSuccess.Append("Your Registration to Mayflower is Successful!").AppendLine();
            builderSuccess.Append("A confirmation email will be sent shortly, kindly check your mailbox.").AppendLine();

            if (message == ManageMessageId.RegisterSuccess)
            {
                ViewBag.StatusMessage = builderSuccess.ToString();
            }
            else if (message == ManageMessageId.RegisterFail)
            {
                ViewBag.StatusMessage = MvcHtmlString.Create("We are very sorry but we seem to have encountered <br /> an issue with your registration." +
                    "<br/>Our administrator is looking into this as soon as possible." +
                    "<br/>We would greatly appreciate if you can try again later.");
            }
            else
            {
                ViewBag.StatusMessage = null;
            }

            return View();
        }

        //
        // POST: /Member/SimpleRegister
        [HttpPost]
        [ValidateAntiForgeryToken]
        [General.AcceptCustomButton(Name = "btnSelfServiceCreateOrganization", Value = "Create")]
        public ActionResult Register(MemberRegisterModels model)
        {
            //20160711 - Remove ModelState Error(required RoleID), set required from Model is because use it for Add User(UserController - CreateAction)

            //ModelState["RoleID"]?.Errors?.Clear();
            //ModelState["PassportExpiryDate"]?.Errors?.Clear();
            //Twin - 2016/12/01 - Not compatible with C# 4.0
            //if (ModelState["RoleID"] != null && ModelState["RoleID"].Errors != null)
            //{
            //    ModelState["RoleID"].Errors.Clear();
            //}
            if (ModelState["PassportExpiryDate"] != null && ModelState["PassportExpiryDate"].Errors != null)
            {
                ModelState["PassportExpiryDate"].Errors.Clear();
            }
            if (ModelState["DOB"] != null && ModelState["DOB"].Errors != null)
            {
                ModelState["DOB"].Errors.Clear();
            }
            if (ModelState["agreeTnC"] != null && ModelState["DOB"].Errors != null)
            {
                ModelState["agreeTnC"].Errors.Clear();
            }
            if (ModelState.IsValid)
            {
                using (var dbContextTransaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        // Initialize Passport Name as First Name
                        model.PassportName = (model.FirstName + " " + model.LastName).Trim();
                        // ADO having problem here, temp implement EF first
                        //MemberServiceController.InsertMember(model, command);

                        if (!string.IsNullOrWhiteSpace(model.ActivationCode))
                        {
                            bool isValidCode = MemberServiceController.CheckIsActivationCodeValid(model.ActivationCode);
                            if (!isValidCode)
                            {
                                ModelState.AddModelError("ActivationCode", MemberServiceController.GetActivationCodeMarketingMsg(model.ActivationCode));
                                return View(model);
                            }

                            SqlCommand command = new SqlCommand();
                            MemberServiceController.InserMemberWithActivationCode(model, command);
                            command.Transaction.Commit();
                        }
                        else
                        {
                            usp_UserInsert_Member(model);
                        }
                        GenerateActivateMail(model.FirstName, model.Email);
                        dbContextTransaction.Commit();

                        if (Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("Apps.Environment")?.ToLower() == "production")
                        {
                            var cookiesForDMP = new HttpCookie("advenuedmp")
                            {
                                Expires = DateTime.Now.AddMinutes(5),
                                Value = "1"
                            };

                            Response.SetCookie(cookiesForDMP);
                        }

                        return RedirectToAction("SuccessRegister", new { Message = ManageMessageId.RegisterSuccess });
                    }
                    catch (Exception ex)
                    {
                        if (dbContextTransaction != null)
                        {
                            dbContextTransaction.Rollback();
                        }

                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine();
                        sb.AppendLine("OrganizationName    : " + model.CompanyName);
                        sb.AppendLine("Email               : " + model.Email);
                        sb.AppendLine("Password            : " + model.ConfirmPassword);
                        sb.AppendLine("FirstName           : " + model.FirstName);
                        sb.AppendLine("LastName            : " + model.LastName);
                        sb.AppendLine("PassportName        : " + model.PassportName);
                        sb.AppendLine("MobilePhone         : " + model.PrimaryPhone);
                        sb.AppendLine("CountryCode           : " + model.CountryCode);
                        sb.AppendLine("IsActive            : " + model.IsActive);

                        Logger logger = LogManager.GetCurrentClassLogger();
                        logger.Error(ex, sb.ToString(), null);

                        return RedirectToAction("Register", new { Message = ManageMessageId.RegisterFail });
                        //throw ex;
                    }
                }
            }

            return View(model);
        }

        //
        // GET: /Member/Register
        public ActionResult SimpleRegister(ManageMessageId? message)
        {
            StringBuilder builderSuccess = new StringBuilder();
            builderSuccess.Append("Your Registration to Mayflower is Successful!").AppendLine();
            builderSuccess.Append("A confirmation email will be sent shortly, kindly check your mailbox.").AppendLine();

            if (message == ManageMessageId.RegisterSuccess)
            {
                ViewBag.StatusMessage = builderSuccess.ToString();
            }
            else if (message == ManageMessageId.RegisterFail)
            {
                ViewBag.StatusMessage = MvcHtmlString.Create("We are very sorry but we seem to have encountered <br /> an issue with your registration." +
                    "<br/>Our administrator is looking into this as soon as possible." +
                    "<br/>We would greatly appreciate if you can try again later.");
            }
            else
            {
                ViewBag.StatusMessage = null;
            }

            return View();
        }

        //
        // POST: /Member/SimpleRegister
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SimpleRegister(MemberRegisterModels model)
        {
            //20160711 - Remove ModelState Error(required RoleID), set required from Model is because use it for Add User(UserController - CreateAction)        
            if (ModelState["FirstName"] != null)
            {
                ModelState["FirstName"].Errors.Clear();
            }
            if (ModelState["LastName"] != null)
            {
                ModelState["LastName"].Errors.Clear();
            }
            /*if (ModelState["Password"] != null)
            {
                ModelState["Password"].Errors.Clear();
            }*/

            if (ModelState.IsValid)
            {
                var userRegistered = db.Users.Where(x => x.Email.ToString() == model.Email.ToString());
                if ((userRegistered.Count() > 0) && (userRegistered.Any(x => x.UserTypeCode == "MEM" || x.UserTypeCode == "AGT")))
                {
                    if (userRegistered.Any(x => x.UserTypeCode == "MEM"))
                    {
                        ModelState.AddModelError("Email", "Email address already exists.");
                    }
                    else if (userRegistered.Any(x => x.UserTypeCode == "AGT"))
                    {
                        ModelState.AddModelError("Email", "You're already registered as a Agent.");
                    }
                }
                else
                {
                    var command = new SqlCommand();
                    try
                    {
                        List<string> nameList = new List<string>();
                        nameList.Add(model.FirstName?.Trim() ?? "");
                        nameList.Add(model.LastName?.Trim() ?? "");
                        nameList.RemoveAll(x => x == string.Empty);

                        model.PassportName = string.Join(" ", nameList);

                        int insertedUserId = MemberServiceController.InsertSimpleMember(model, command);

                        if (command?.Transaction != null)
                        {
                            command.Transaction.Commit();

                            db = db.DisposeAndRefresh();
                            var _userInserted = db.Users.FirstOrDefault(x => x.UserID == insertedUserId);
                            string pwdSalt = Core.GeneratePasswordSalt();
                            _userInserted.Pwd = Core.Encrypt(model.Password, pwdSalt);
                            _userInserted.PwdSalt = pwdSalt;
                            db.SaveChanges();

                            bool sendStatusSuccess = false; // try send email 3 times
                            for (int i = 0; i <= 3; i++)
                            {
                                sendStatusSuccess = GenerateSimpleActivateMail(insertedUserId);
                                if (sendStatusSuccess)
                                    break;
                            }

                            if (!sendStatusSuccess) // Send Log 
                                logger.Error("Send Simple Register Email Fail." + JsonConvert.SerializeObject(model, Formatting.Indented));

                            return RedirectToAction("SuccessRegister", new { Message = ManageMessageId.RegisterSuccess });
                        }
                        else
                        {
                            return RedirectToAction("SimpleRegister", new { Message = ManageMessageId.RegisterFail });
                        }
                    }
                    catch (Exception ex)
                    {
                        if (command?.Transaction != null)
                            command.Transaction.Rollback();

                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine();
                        sb.AppendLine("Email               : " + model.Email);
                        sb.AppendLine("Promo Code          : " + model.ActivationCode);

                        logger.Error(ex, sb.ToString());

                        return RedirectToAction("SimpleRegister", new { Message = ManageMessageId.RegisterFail });
                    }
                }
            }

            return View(model);
        }

        [Authorize]
        public ActionResult CardView()
        {
            var modelCaller = new MemberServiceController.ModelInitialize(db);
            var model = modelCaller.PopulateUserProfileEditModel().FirstOrDefault(x => x.UserID == CurrentUserID);

            if (model == null)
            {
                return HttpNotFound();
            }

            return View(model);
        }

        //
        // POST: /Account/ManageProfile
        [Authorize]
        public ActionResult ManageProfile()
        {
            return View();
        }

        //#region Update Profile Layout & Function
        [NoCache]
        [Authorize]
        public ActionResult UpdateProfile()
        {
            var modelCaller = new MemberServiceController.ModelInitialize(db);
            var model = modelCaller.PopulateUserProfileEditModel().FirstOrDefault(x => x.UserID == CurrentUserID);

            if (model == null)
            {
                return HttpNotFound();
            }

            return View(model);
        }

        //// POST: Users/UpdateProfile/
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateProfile(UserProfileEditModel model)
        {
            if (ModelState.IsValid)
            {
                using (var dbContextTransaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        MemberServiceController.UpdateMemberProfile(CurrentUserID, db, model);

                        dbContextTransaction.Commit();
                        return RedirectToAction("ManageProfile", "Member");
                    }
                    catch (Exception ex)
                    {
                        dbContextTransaction.Rollback();
                        Logger logger = LogManager.GetCurrentClassLogger();
                        logger.Error(ex);
                    }
                }
            }

            var modelCaller = new MemberServiceController.ModelInitialize(db);
            model = modelCaller.PopulateUserProfileEditModel().FirstOrDefault(x => x.UserID == CurrentUserID);

            return View(model);
        }

        //#endregion

        [AllowAnonymous]
        public ActionResult UpdateAccount(string token, string email)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invalid Params.");
            }

            ViewBag.Token = token;
            ViewBag.Email = email;

            string useridString = "";
            Cryptography.AES.TryDecrypt(token, out useridString);

            User user = db.Users.FirstOrDefault(u => u.UserID.ToString() == useridString
                            && u.Email.ToLower() == email.ToLower() && u.UserTypeCode == "MEM");

            if (user != null)
                return View();
            else
                return RedirectToAction("Index", "Home", new { error = "account-activated" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("UpdateAccount")]
        public ActionResult UpdateAccount_POST(string token, string email, MemberRegisterModels model)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invalid Params.");
            }

            if (ModelState["DOB"] == null || ModelState["DOB"].Errors.Count == 1)
            {
                ModelState["DOB"].Errors.Clear();
            }

            if (string.IsNullOrWhiteSpace(model.PrimaryPhone))
            {
                ModelState.AddModelError("PrimaryPhone", "Primary Phone is required.");
            }

            TryValidateModel(model);

            if (ModelState.IsValid)
            {
                string useridString = "";
                Cryptography.AES.TryDecrypt(token, out useridString);

                User user = db.Users.FirstOrDefault(u => u.UserID.ToString() == useridString
                                && !u.IsProfileActivated && u.Email.ToLower() == email.ToLower() && u.UserTypeCode == "MEM");

                if (user != null && !user.LastLoginDate.HasValue) // not login before
                {
                    SqlCommand command = new SqlCommand();
                    List<string> error = new List<string>();

                    try
                    {
                        MemberServiceController.UpdateSimpleMember(model, command, user.UserID);

                        if (command?.Transaction != null)
                        {
                            command.Transaction.Commit();
                            db = db.DisposeAndRefresh();

                            bool sendStatus = false;

                            for (int i = 0; i <= 3; i++)
                            {
                                sendStatus = SendVerifiedMail(user);
                                if (sendStatus)
                                    break;
                            }

                            if (!sendStatus)
                                logger.Error("Send verified mail error in UpdateAccount_POST.");
                            else
                            {
                                Mayflower.General.LoginClass login = new Mayflower.General.LoginClass(model.Email, model.Password, db);
                                Response.SetAuthCookie(login.User.UserID.ToString(), false, login.UserData);
                                MemberServiceController.UpdateLoginDate(login.User, db);
                            }

                            return RedirectToAction("Index", "Home", new { email = user.Email.ToLower(), status = "success-validate" });
                            //return RedirectToAction("SuccessValidate", "Member", new { email = user.Email.ToLower() });
                        }
                        else
                        {
                            ModelState.AddModelError("Error", "Unexpected system error, please try again later.");
                        }
                    }
                    catch (AggregateException ae)
                    {
                        if (command?.Transaction != null)
                            command.Transaction.Rollback();

                        model.Password = ReplaceAllToHash(model.Password);
                        model.ConfirmPassword = ReplaceAllToHash(model.ConfirmPassword);

                        error.Add("UpdateAccount_POST AggregateException Error.");
                        logger.Error(ae, "UpdateAccount_POST AggregateException Error." + Environment.NewLine + Environment.NewLine
                            + JsonConvert.SerializeObject(model));
                    }
                    catch (Exception ex)
                    {
                        if (command?.Transaction != null)
                            command.Transaction.Rollback();

                        model.Password = ReplaceAllToHash(model.Password);
                        model.ConfirmPassword = ReplaceAllToHash(model.ConfirmPassword);

                        error.Add("UpdateAccount_POST Exception Error.");
                        logger.Error(ex, "UpdateAccount_POST Exception Error." + Environment.NewLine + Environment.NewLine
                            + JsonConvert.SerializeObject(model));
                    }

                    if (error.Count > 0)
                    {
                        ModelState.AddModelError("Error", "Unexpected system error, please try again later.");
                    }
                }
                else
                {
                    return RedirectToAction("Index", "Home", new { error = "account-activated" });
                }
            }

            ViewBag.Token = token;
            ViewBag.Email = email;

            return View(model);
        }

        [HttpPost]
        //[AcceptCustomButton(Name = "btnChkCode", Value = "Apply")]
        public ActionResult ValidActivationCode(MemberRegisterModels model)
        {
            if (ModelState.IsValidField("ActivationCode"))
            {
                string activationCodeMsg = null;
                bool isValid = true;
                if (!string.IsNullOrWhiteSpace(model.ActivationCode))
                {
                    activationCodeMsg = MemberServiceController.GetActivationCodeMarketingMsg(model.ActivationCode);
                    isValid = MemberServiceController.CheckIsActivationCodeValid(model.ActivationCode);
                }

                return Json(new { result = activationCodeMsg, status = isValid });
            }
            else
            {
                return Json(new { result = "Unexpected referral code format.", status = false });
            }
        }

        #region System Generated Active Link Functions
        public async Task<ActionResult> OTPActivation(string token, string email, string t, string uid)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(t) || string.IsNullOrWhiteSpace(uid))
            {
                throw new HttpException(404, "Not Found");
            }
            else
            {
                try
                {
                    dynamic obj = JsonConvert.DeserializeObject(Cryptography.AES.Decrypt(token));

                    string dec_Email = obj.Email;
                    int dec_UserID = obj.UID;
                    int dec_OrgID = obj.OID;
                    string dec_UserType = obj.UType;
                    DateTime dec_ValidDate = obj.RequestUTCDate;
                    int dec_LinkValidHour = obj.TokenValidateHour;

                    bool isValidRequest = dec_ValidDate.AddHours((double)dec_LinkValidHour) <= DateTime.UtcNow ? false : true;

                    if (isValidRequest)
                    {
                        MayFlower db = new MayFlower();
                        var requestUser = db.Users.FirstOrDefault(x => x.UserID == dec_UserID && x.OrganizationID == dec_OrgID);

                        if (requestUser.IsActive && requestUser.IsProfileActivated && !requestUser.IsResetLinkUsed && requestUser.LoginAttempt == 0)
                        {
                            string redirectUrl = Core.EnableCMS ? System.Web.Configuration.WebConfigurationManager.AppSettings["AlphaReds.CMSUrl"]
                                : Url.Action("index", "home", null, Request.Url.Scheme);

                            // used link, due to no store physical link. use this kind of method as hotfix.
                            return RedirectToAction("login", "account", new { loginerror = "Used link, account reactivated.", returnUrl = redirectUrl });
                        }
                        else
                        {
                            requestUser.IsActive = true;
                            requestUser.IsProfileActivated = true;
                            requestUser.IsResetLinkUsed = false;
                            requestUser.LoginAttempt = 0;
                            requestUser.ModifiedDate = DateTime.Now;
                            requestUser.ModifiedDateUTC = DateTime.UtcNow;
                            await db.SaveChangesAsync();
                            return Content("Account reactivated. <br/><a href='" + Url.Action("login", "account", new { @ref = "optactived" }, Request.Url.Scheme) + "'>Click Here to Login.</a>");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "[" + DateTime.Now.ToLoggerDateTime() + "]" + "OTPActivation fail. Email - " + email);
                    throw new HttpException(404, "Not Found");
                }
                throw new HttpException(404, "Not Found");
            }
        }

        [Authorize]
        [HttpPost]
        public ActionResult ResendVerificationEmail(string token, string email, string t, string uid)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(t) || string.IsNullOrWhiteSpace(uid))
            {
                throw new HttpException(404, "Not Found");
            }
            else
            {
                try
                {
                    dynamic obj = JsonConvert.DeserializeObject(Cryptography.AES.Decrypt(token));

                    string dec_Email = obj.Email;
                    int dec_UserID = obj.UID;
                    int dec_OrgID = obj.OID;
                    string dec_UserType = obj.UType;
                    DateTime dec_ValidDate = obj.RequestUTCDate;
                    int dec_LinkValidHour = obj.TokenValidateHour;

                    bool isValidRequest = dec_ValidDate.AddHours((double)dec_LinkValidHour) <= DateTime.UtcNow ? false : true;

                    if (isValidRequest)
                    {
                        MayFlower db = new MayFlower();
                        var requestUser = db.Users.FirstOrDefault(x => x.UserID == dec_UserID && x.OrganizationID == dec_OrgID);

                        if (requestUser.IsActive && requestUser.IsProfileActivated && !requestUser.IsResetLinkUsed && requestUser.LoginAttempt == 0)
                        {
                            string redirectUrl = Core.EnableCMS ? System.Web.Configuration.WebConfigurationManager.AppSettings["AlphaReds.CMSUrl"]
                                : Url.Action("index", "home", null, Request.Url.Scheme);

                            // used link, due to no store physical link. use this kind of method as hotfix.
                            return RedirectToAction("login", "account", new { loginerror = "Used link, account reactivated.", returnUrl = redirectUrl });
                        }
                        else
                        {
                            bool sendStatus = GenerateSimpleActivateMail(requestUser.UserID);
                            return JavaScript(@"$('#popup-modal #modal-container').html('Resend activation mail, please check your mailbox.');
                                                $('#popup-modal').show();");
                            return Json(sendStatus);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "[" + DateTime.Now.ToLoggerDateTime() + "]" + "ResendVerificationEmail fail. Email - " + email);
                    throw new HttpException(404, "Not Found");
                }
                throw new HttpException(404, "Not Found");
            }
        }
        #endregion

        #region Internal Local Functions
        [LocalhostFilter]
        public ActionResult GenerateActiveDisabledAccountLink(string email, string role)
        {
            if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(email))
            {
                return Content("Param missed. Please type in proper info.");
            }

            MayFlower db = new MayFlower();
            var user = db.Users.FirstOrDefault(x => x.Email.ToLower() == email && x.UserTypeCode.ToLower() == role);

            if (user == null)
            {
                return Content("User Not Found.");
            }

            int tokenValidateHour = 24;
            int.TryParse(Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("TokenValidateHour"), out tokenValidateHour);

            string token = MemberServiceController.GenerateToken(user, tokenValidateHour);

            string url = Url.Action("otpactivation", "member", new { t = 1, uid = Guid.NewGuid().ToString(), token, email }, Request.Url.Scheme);
            return Content(string.Format("<div style='word-wrap: break-word;'><a href='{0}'>{0}</a></div>", url));
        }
        #endregion

        [ChildActionOnly]
        public ActionResult LoadSessionTransferUserData()
        {
            string memberId = Request.Form["SessionTransferMemberId"];

            if (!String.IsNullOrWhiteSpace(memberId))
            {
                int userId = -1;
                int.TryParse(memberId, out userId);

                if (userId > 0)
                {
                    using (db)
                    {
                        // CSCHONG Old Code, Get Member Detail to Transfer.
                        /* 2017/11/10 - Heng Remarks, should not use this method pass session.
                         * Potential malicious user user post form only userid to simulate login. */
                        var user = db.Users.FirstOrDefault(u => u.UserID == userId && u.UserTypeCode == "MEM");
                        if (user != null)
                        {
                            Mayflower.General.LoginClass login = new General.LoginClass(user.Email, user.Pwd, db);
                            var userData = login.UserData;

                            Response.SetAuthCookie(memberId, false, userData);
                        }
                    }
                }
            }

            return PartialView();
        }

        public void GenerateActivateMail(string userName, string registeredEmail)
        {
            //retreive new inserted Traveller Admin
            User user = db.Users.FirstOrDefault(m => m.Email == registeredEmail && m.UserTypeCode == "MEM");

            if (user != null)
            {
                string registeredUserID = General.CustomizeBaseEncoding.CodeBase64(user.UserID.ToString());
                var emailaddress = user.Email.ToString();

                Hashtable ht = new Hashtable();
                ht.Add("<#UserName>", userName);
                //ht.Add("<#UserID>", Url.Action("ActivateByEmailLink", "Member", new { Token = registeredUserID, Email = RegisteredEmail }));
                ht.Add("<#Token>", registeredUserID);
                ht.Add("<#Email>", registeredEmail);

                CommonServiceController.SendEmail(emailaddress, "Mayflower needs you to verify your account!", Alphareds.Module.Common.Core.getMailTemplate("travellerregister", ht));
            }
            else
            {
                throw new HttpException(404, "Not Found");
            }
        }

        public bool GenerateActivateMail(string userName, int userId)
        {
            //retreive new inserted Traveller Admin
            User user = db.Users.FirstOrDefault(m => m.UserID == userId);

            if (user != null)
            {
                string registeredUserID = Cryptography.AES.Encrypt(userId.ToString());
                var emailaddress = user.Email.ToString();

                Hashtable ht = new Hashtable();
                ht.Add("<#UserName>", userName);
                //ht.Add("<#UserID>", Url.Action("ActivateByEmailLink", "Member", new { Token = registeredUserID, Email = RegisteredEmail }));
                ht.Add("<#Token>", registeredUserID);
                ht.Add("<#Email>", user.Email);

                return CommonServiceController.SendEmail(emailaddress, "Mayflower needs you to verify your account!", Alphareds.Module.Common.Core.getMailTemplate("travellerregister", ht));
            }
            else
            {
                throw new HttpException(404, "Not Found");
            }
        }


        protected bool GenerateSimpleActivateMail(int insertedUID)
        {
            //retreive new inserted Traveller Admin
            User user = db.Users.FirstOrDefault(m => m.UserID == insertedUID);

            if (user != null)
            {
                string registeredUserID = Cryptography.AES.Encrypt(user.UserID.ToString());
                var emailaddress = user.Email.ToString();

                Hashtable ht = new Hashtable();
                ht.Add("<#ActiveURL>", Url.Action("SimpleActivateByEmailLink", "Member", new { token = registeredUserID, Email = user.Email, atype = "nopass" })); ;

                return CommonServiceController.SendEmail(emailaddress, "Mayflower needs you to update your account!", General.Core.getMailTemplate("travellerregister_simple", ht));
            }
            else
            {
                throw new HttpException(404, "Not Found");
            }
        }

        public void GenerateSimpleActivateMail(string RegisteredEmail, bool aesEncrypt)
        {
            //retreive new inserted Traveller Admin
            User user = db.Users.FirstOrDefault(m => m.Email == RegisteredEmail);

            if (user != null)
            {
                string registeredUserID = Cryptography.AES.Encrypt(user.UserID.ToString());
                var emailaddress = user.Email.ToString();

                Hashtable ht = new Hashtable();
                ht.Add("<#ActiveURL>", Url.Action("SimpleActivateByEmailLink", "Member", new { Token = registeredUserID, Email = RegisteredEmail, atype = "nopass" })); ;

                CommonServiceController.SendEmail(emailaddress, "Mayflower needs you to update your account!", General.Core.getMailTemplate("travellerregister_simple", ht));
            }
            else
            {
                throw new HttpException(404, "Not Found");
            }
        }

        protected bool SendVerifiedMail(User user)
        {
            //string registeredUserID = General.CustomizeBaseEncoding.CodeBase64(user.UserID.ToString());
            var emailaddress = user.Email.ToString();

            Hashtable ht = new Hashtable();
            /*ht.Add("<#UserName>", user.FullName);
            ht.Add("<#Email>", user.Email);*/

            return CommonServiceController.SendEmail(emailaddress, "You did it, buddy! Welcome aboard to the Mayflower community. ", General.Core.getMailTemplate("memberverified", ht));
        }


        public void GenerateVerifiedMail(string UserName, string RegisteredEmail)
        {
            //retreive new inserted Traveller Admin
            User user = db.Users.FirstOrDefault(m => m.Email == RegisteredEmail);

            if (user != null)
            {
                string registeredUserID = General.CustomizeBaseEncoding.CodeBase64(user.UserID.ToString());
                var emailaddress = user.Email.ToString();

                Hashtable ht = new Hashtable();
                ht.Add("<#UserName>", UserName);
                ht.Add("<#Email>", RegisteredEmail);

                CommonServiceController.SendEmail(emailaddress, "You did it, buddy! Welcome aboard to the Mayflower community. ", General.Core.getMailTemplate("memberverified", ht));
            }
            else
            {
                throw new HttpException(404, "Not Found");
            }
        }

        public RedirectResult CheckEmailLinkClicked(string url, int orderId = -1, int userID = -1, int activeUserID = -1, string FunctionType = null) /**/
        {
            Alphareds.Module.Data.MYSQL mySqlDB = new Alphareds.Module.Data.MYSQL(ConfigurationManager.ConnectionStrings["MySqlConnector"].ConnectionString.ToString());

            string environment = Core.GetAppSettingValueEnhanced("Apps.Environment");
            int logId = -1;

            try
            {
                if (environment?.ToLower() != "production") //staging
                {
                    if (orderId != -1 && FunctionType == "insurance")
                    {
                        string decodeUrl = System.Web.HttpUtility.UrlDecode(url);
                        url = decodeUrl;
                        var result = mySqlDB.ExecuteQueryTable(string.Format("SELECT EmailMarketingQueueLogID from Stg_EmailMarketingQueueLog WHERE OrderID = {0} and FunctionType = 'CrossSalesIns' ORDER BY EmailMarketingQueueLogID DESC LIMIT 1;",
                            orderId
                            ));
                        if (result != null && result.Rows.Count > 0)
                        {
                            logId = Int32.Parse(result.Rows[0][0].ToString());
                        }
                    }
                    else if (orderId != -1 && FunctionType == "CrossSalesFlight")
                    {
                        var result = mySqlDB.ExecuteQueryTable(string.Format("SELECT EmailMarketingQueueLogID from Stg_EmailMarketingQueueLog WHERE OrderID = {0} ORDER BY EmailMarketingQueueLogID DESC LIMIT 1;",
                            orderId
                            ));
                        if (result != null && result.Rows.Count > 0)
                        {
                            logId = Int32.Parse(result.Rows[0][0].ToString());
                        }
                    }
                    else if (userID != -1 && FunctionType == "TravelCreditExpired")
                    {
                        var result = mySqlDB.ExecuteQueryTable(string.Format("SELECT EmailMarketingQueueLogID from Stg_EmailMarketingQueueLog WHERE UserID = {0} and FunctionType = 'TravelCreditExpire' ORDER BY EmailMarketingQueueLogID DESC LIMIT 1;",
                            userID
                            ));
                        if (result != null && result.Rows.Count > 0)
                        {
                            logId = Int32.Parse(result.Rows[0][0].ToString());
                        }
                    }
                    else if (activeUserID != -1 && FunctionType == "ActivationReminder")
                    {
                        string decodeUrl = System.Web.HttpUtility.UrlDecode(url);
                        url = decodeUrl;
                        var result = mySqlDB.ExecuteQueryTable(string.Format("SELECT EmailMarketingQueueLogID from Stg_EmailMarketingQueueLog WHERE UserID = {0} and FunctionType = 'MemberActivate' ORDER BY EmailMarketingQueueLogID DESC LIMIT 1;",
                            activeUserID
                            ));
                        if (result != null && result.Rows.Count > 0)
                        {
                            logId = Int32.Parse(result.Rows[0][0].ToString());
                        }
                    }
                    //CALL SP to update MYSQL
                    if (logId != -1)
                    {
                        mySqlDB.ExecuteNonQuery(string.Format("Call usp_Stg_EmailMarketingQueueLogUpdate( {0}, {1});",
                            logId,
                            true
                            ));
                    }
                }
                else if (environment?.ToLower() == "production") //production
                {
                    if (orderId != -1 && FunctionType == "insurance")
                    {
                        string decodeUrl = System.Web.HttpUtility.UrlDecode(url);
                        url = decodeUrl;
                        var result = mySqlDB.ExecuteQueryTable(string.Format("SELECT EmailMarketingQueueLogID from EmailMarketingQueueLog WHERE OrderID = {0} and FunctionType = 'CrossSalesIns' ORDER BY EmailMarketingQueueLogID DESC LIMIT 1;",
                            orderId
                            ));
                        if (result != null && result.Rows.Count > 0)
                        {
                            logId = Int32.Parse(result.Rows[0][0].ToString());
                        }
                    }
                    else if (orderId != -1 && FunctionType == "CrossSalesFlight")
                    {
                        var result = mySqlDB.ExecuteQueryTable(string.Format("SELECT EmailMarketingQueueLogID from EmailMarketingQueueLog WHERE OrderID = {0} ORDER BY EmailMarketingQueueLogID DESC LIMIT 1;",
                            orderId
                            ));
                        if (result != null && result.Rows.Count > 0)
                        {
                            logId = Int32.Parse(result.Rows[0][0].ToString());
                        }
                    }
                    else if (userID != -1 && FunctionType == "TravelCreditExpired")
                    {
                        var result = mySqlDB.ExecuteQueryTable(string.Format("SELECT EmailMarketingQueueLogID from EmailMarketingQueueLog WHERE UserID = {0} and FunctionType = 'TravelCreditExpire' ORDER BY EmailMarketingQueueLogID DESC LIMIT 1;",
                            userID
                            ));
                        if (result != null && result.Rows.Count > 0)
                        {
                            logId = Int32.Parse(result.Rows[0][0].ToString());
                        }
                    }
                    else if (activeUserID != -1 && FunctionType == "ActivationReminder")
                    {
                        string decodeUrl = System.Web.HttpUtility.UrlDecode(url);
                        url = decodeUrl;
                        var result = mySqlDB.ExecuteQueryTable(string.Format("SELECT EmailMarketingQueueLogID from EmailMarketingQueueLog WHERE UserID = {0} and FunctionType = 'MemberActivate' ORDER BY EmailMarketingQueueLogID DESC LIMIT 1;",
                            activeUserID
                            ));
                        if (result != null && result.Rows.Count > 0)
                        {
                            logId = Int32.Parse(result.Rows[0][0].ToString());
                        }
                    }
                    //CALL SP to update MYSQL
                    if (logId != -1)
                    {
                        mySqlDB.ExecuteNonQuery(string.Format("Call usp_EmailMarketingQueueLogUpdate( {0}, {1});",
                            logId,
                            true
                            ));
                    }
                }

            }
            catch (Exception ex)
            {
                logger.Error(ex, "CheckEmailLinkClicked Exception Error." + Environment.NewLine + Environment.NewLine);
            }

            return new RedirectResult(url);
        }

        private void usp_UserInsert_SimplifiedMember(MemberRegisterModels model)
        {
            //string PasswordSalt = Core.GeneratePasswordSalt();

            var query = string.Empty;
            query = "Exec Users.usp_UserInsert_SimplifiedMember @Email, @TitleCode, @TempRefCode,";
            query += "@PrimaryPhoneCountryCode, @SecondaryPhoneCountryCode, @AirlineCode, @FrequentFlyerNumber,";
            query += "@CreatedByID, @ModifiedByID, @IsActive, @IsActivated";

            db.Database.ExecuteSqlCommand(query,
                //new SqlParameter("PwdSalt", !string.IsNullOrEmpty(PasswordSalt) ? PasswordSalt : (object)DBNull.Value),
                new SqlParameter("Email", !string.IsNullOrEmpty(model.Email) ? model.Email : (object)DBNull.Value),
                new SqlParameter("TitleCode", !string.IsNullOrEmpty(model.TitleCode) ? model.TitleCode : "NA "),
                new SqlParameter("TempRefCode", !string.IsNullOrEmpty(model.ActivationCode) ? model.ActivationCode : (object)DBNull.Value),
                new SqlParameter("PrimaryPhoneCountryCode", !string.IsNullOrEmpty(model.PhoneCode1) ? model.PhoneCode1 : "NA"),
                new SqlParameter("SecondaryPhoneCountryCode", !string.IsNullOrEmpty(model.PhoneCode2) ? model.PhoneCode2 : "NA"),
                new SqlParameter("AirlineCode", !string.IsNullOrEmpty(model.FrequentFlyerNumber) ? model.FrequentFlyerAirlineCode : (object)DBNull.Value),
                new SqlParameter("FrequentflyerNumber", !string.IsNullOrEmpty(model.FrequentFlyerNumber) ? model.FrequentFlyerNumber : (object)DBNull.Value),
                new SqlParameter("CreatedByID", CurrentUserID),
                new SqlParameter("ModifiedByID", CurrentUserID),
                new SqlParameter("IsActive", true), // use for indicate delete only
                new SqlParameter("IsActivated", false)); // new user require to set their profile active.

            db.SaveChanges();
        }

        private void usp_SimplifiedUserUpdate(int userID, MemberRegisterModels model)
        {
            int OrganizationID;
            Organization organization = db.Organizations.FirstOrDefault(o => o.OrganizationName == model.CompanyName);
            OrganizationID = organization != null ? organization.OrganizationID : 0;
            User user = db.Users.FirstOrDefault(u => u.UserID == userID);
            string PasswordSalt = Core.GeneratePasswordSalt();

            var query = string.Empty;
            query = "Exec Users.usp_SimplifiedUserUpdate @UserID, @TitleCode, @Pwd, @PwdSalt, @FirstName, @LastName, @PrimaryPhoneCountryCode, @PrimaryPhone,";
            query += "@SecondaryPhoneCountryCode, @SecondaryPhone, @DOB, @IdentificationNumber, @PassportNumber, @PassportExpiryDate,";
            query += "@PassportIssuingCountry, @Address1, @Address2, @City,	@PostalCode, @State, @Country, @CompanyName, @CompanyAddress1,";
            query += "@CompanyAddress2, @CompanyCity, @CompanyPostalCode, @CompanyState, @CompanyCountry, @CreatedByID, @ModifiedByID, @IsActivated, @IsActive";

            db.Database.ExecuteSqlCommand(query,
                new SqlParameter("UserID", userID), // pass user id here, need to pass in controller POST action
                new SqlParameter("TitleCode", !string.IsNullOrEmpty(model.TitleCode) ? model.TitleCode : (object)DBNull.Value),
                new SqlParameter("PwdSalt", !string.IsNullOrEmpty(PasswordSalt) ? PasswordSalt : (object)DBNull.Value),
                new SqlParameter("Pwd", !string.IsNullOrEmpty(model.Password) ? Core.Encrypt(model.Password, PasswordSalt).ToString() : (object)DBNull.Value),
                new SqlParameter("FirstName", !string.IsNullOrEmpty(model.FirstName) ? model.FirstName.Trim() : (object)DBNull.Value),
                new SqlParameter("LastName", !string.IsNullOrEmpty(model.LastName) ? model.LastName.Trim() : (object)DBNull.Value),
                new SqlParameter("PrimaryPhoneCountryCode", !string.IsNullOrEmpty(model.PhoneCode1) ? model.PhoneCode1 : "NA"),
                new SqlParameter("PrimaryPhone", !string.IsNullOrEmpty(model.PrimaryPhone) ? model.PrimaryPhone : (object)DBNull.Value),
                new SqlParameter("SecondaryPhone", !string.IsNullOrEmpty(model.SecondaryPhone) ? model.SecondaryPhone : (object)DBNull.Value),
                new SqlParameter("SecondaryPhoneCountryCode", !string.IsNullOrEmpty(model.PhoneCode2) ? model.PhoneCode2 : "NA"),
                new SqlParameter("DOB", model.DOB == DateTime.MinValue ? (object)DBNull.Value : model.DOB),
                new SqlParameter("IdentificationNumber", !string.IsNullOrEmpty(model.IdentityNo) ? model.IdentityNo : (object)DBNull.Value),
                new SqlParameter("PassportNumber", !string.IsNullOrEmpty(model.PassportNo) ? model.PassportNo : (object)DBNull.Value),
                new SqlParameter("PassportExpiryDate", model.PassportExpiryDate == null || model.PassportExpiryDate == DateTime.MinValue ? (object)DBNull.Value : model.PassportExpiryDate),
                new SqlParameter("PassportIssuingCountry", !string.IsNullOrEmpty(model.PassportIssuePlace) ? model.PassportIssuePlace : (object)DBNull.Value),
                new SqlParameter("Address1", !string.IsNullOrEmpty(model.Address1) ? model.Address1 : (object)DBNull.Value),
                new SqlParameter("Address2", !string.IsNullOrEmpty(model.Address2) ? model.Address2 : (object)DBNull.Value),
                new SqlParameter("City", !string.IsNullOrEmpty(model.City) ? model.City : (object)DBNull.Value),
                new SqlParameter("PostalCode", !string.IsNullOrEmpty(model.Postcode) ? model.Postcode : (object)DBNull.Value),
                new SqlParameter("State", !string.IsNullOrEmpty(model.AddressProvinceState) ? model.AddressProvinceState : (object)DBNull.Value),
                new SqlParameter("Country", !string.IsNullOrEmpty(model.CountryCode) ? model.CountryCode : (object)DBNull.Value),
                new SqlParameter("CompanyName", !string.IsNullOrEmpty(model.CompanyName) ? model.CompanyName : (object)DBNull.Value),
                new SqlParameter("CompanyAddress1", !string.IsNullOrEmpty(model.CompanyAddress1) ? model.CompanyAddress1 : (object)DBNull.Value),
                new SqlParameter("CompanyAddress2", !string.IsNullOrEmpty(model.CompanyAddress2) ? model.CompanyAddress2 : (object)DBNull.Value),
                new SqlParameter("CompanyCity", !string.IsNullOrEmpty(model.CompanyCity) ? model.CompanyCity : (object)DBNull.Value),
                new SqlParameter("CompanyPostalCode", !string.IsNullOrEmpty(model.CompanyPostcode) ? model.CompanyPostcode : (object)DBNull.Value),
                new SqlParameter("CompanyState", !string.IsNullOrEmpty(model.CompanyAddressProvinceState) ? model.CompanyAddressProvinceState : (object)DBNull.Value),
                new SqlParameter("CompanyCountry", !string.IsNullOrEmpty(model.CompanyAddressCountryCode) ? model.CompanyAddressCountryCode : (object)DBNull.Value),
                new SqlParameter("CreatedByID", model.CreatedByID),
                new SqlParameter("ModifiedByID", model.ModifiedByID),
                new SqlParameter("IsActivated", true),
                new SqlParameter("IsActive", true)); // default is TRUE, purpose use for indicate deleted

            db.SaveChanges();
        }

        private void usp_UserInsert_Member(MemberRegisterModels model)
        {
            int OrganizationID;
            Organization organization = db.Organizations.FirstOrDefault(o => o.OrganizationName == model.CompanyName);
            OrganizationID = organization != null ? organization.OrganizationID : 0;

            string PasswordSalt = Core.GeneratePasswordSalt();

            var query = string.Empty;
            query = "Exec Users.usp_UserInsert_Member @Pwd, @PwdSalt, @Email, @TitleCode, @FirstName, @LastName, @PassportName, @PrimaryPhoneCountryCode, @PrimaryPhone,";
            query += "@SecondaryPhoneCountryCode, @SecondaryPhone, @DOB, @IdentificationNumber, @AirlineCode, @FrequentFlyerNumber, @PassportNumber, @PassportExpiryDate,";
            query += "@PassportIssuingCountry, @Address1, @Address2, @City,	@PostalCode, @State, @Country, @CompanyName, @CompanyAddress1,";
            query += "@CompanyAddress2, @CompanyCity, @CompanyPostalCode, @CompanyState, @CompanyCountry, @CreatedByID, @ModifiedByID, @IsActive";

            db.Database.ExecuteSqlCommand(query,
                new SqlParameter("Pwd", !string.IsNullOrEmpty(model.Password) ? Core.Encrypt(model.Password, PasswordSalt) : (object)DBNull.Value),
                new SqlParameter("PwdSalt", !string.IsNullOrEmpty(PasswordSalt) ? PasswordSalt : (object)DBNull.Value),
                new SqlParameter("Email", !string.IsNullOrEmpty(model.Email) ? model.Email : (object)DBNull.Value),
                new SqlParameter("TitleCode", !string.IsNullOrEmpty(model.TitleCode) ? model.TitleCode : (object)DBNull.Value),
                new SqlParameter("FirstName", !string.IsNullOrEmpty(model.FirstName) ? model.FirstName.Trim() : (object)DBNull.Value),
                new SqlParameter("LastName", !string.IsNullOrEmpty(model.LastName) ? model.LastName.Trim() : (object)DBNull.Value),
                new SqlParameter("PassportName", !string.IsNullOrEmpty(model.PassportName) ? model.PassportName.Trim() : (object)DBNull.Value),
                new SqlParameter("PrimaryPhoneCountryCode", !string.IsNullOrEmpty(model.PhoneCode1) ? model.PhoneCode1 : "NA"),
                new SqlParameter("PrimaryPhone", !string.IsNullOrEmpty(model.PrimaryPhone) ? model.PrimaryPhone : (object)DBNull.Value),
                new SqlParameter("SecondaryPhoneCountryCode", !string.IsNullOrEmpty(model.PhoneCode2) ? model.PhoneCode2 : "NA"),
                new SqlParameter("SecondaryPhone", !string.IsNullOrEmpty(model.SecondaryPhone) ? model.SecondaryPhone : (object)DBNull.Value),
                new SqlParameter("DOB", model.DOB == DateTime.MinValue ? (object)DBNull.Value : model.DOB),
                new SqlParameter("IdentificationNumber", !string.IsNullOrEmpty(model.IdentityNo) ? model.IdentityNo : (object)DBNull.Value),
                new SqlParameter("AirlineCode", !string.IsNullOrEmpty(model.FrequentFlyerNumber) ? model.FrequentFlyerAirlineCode : (object)DBNull.Value),
                new SqlParameter("FrequentflyerNumber", !string.IsNullOrEmpty(model.FrequentFlyerNumber) ? model.FrequentFlyerNumber : (object)DBNull.Value),
                new SqlParameter("PassportNumber", !string.IsNullOrEmpty(model.PassportNo) ? model.PassportNo : (object)DBNull.Value),
                new SqlParameter("PassportExpiryDate", model.PassportExpiryDate == null || model.PassportExpiryDate == DateTime.MinValue ? (object)DBNull.Value : model.PassportExpiryDate),
                new SqlParameter("PassportIssuingCountry", !string.IsNullOrEmpty(model.PassportIssuePlace) ? model.PassportIssuePlace : (object)DBNull.Value),
                new SqlParameter("Address1", !string.IsNullOrEmpty(model.Address1) ? model.Address1 : (object)DBNull.Value),
                new SqlParameter("Address2", !string.IsNullOrEmpty(model.Address2) ? model.Address2 : (object)DBNull.Value),
                new SqlParameter("City", !string.IsNullOrEmpty(model.City) ? model.City : (object)DBNull.Value),
                new SqlParameter("PostalCode", !string.IsNullOrEmpty(model.Postcode) ? model.Postcode : (object)DBNull.Value),
                new SqlParameter("State", !string.IsNullOrEmpty(model.AddressProvinceState) ? model.AddressProvinceState : (object)DBNull.Value),
                new SqlParameter("Country", !string.IsNullOrEmpty(model.CountryCode) ? model.CountryCode : (object)DBNull.Value),
                new SqlParameter("CompanyName", !string.IsNullOrEmpty(model.CompanyName) ? model.CompanyName : (object)DBNull.Value),
                new SqlParameter("CompanyAddress1", !string.IsNullOrEmpty(model.CompanyAddress1) ? model.CompanyAddress1 : (object)DBNull.Value),
                new SqlParameter("CompanyAddress2", !string.IsNullOrEmpty(model.CompanyAddress2) ? model.CompanyAddress2 : (object)DBNull.Value),
                new SqlParameter("CompanyCity", !string.IsNullOrEmpty(model.CompanyCity) ? model.CompanyCity : (object)DBNull.Value),
                new SqlParameter("CompanyPostalCode", !string.IsNullOrEmpty(model.CompanyPostcode) ? model.CompanyPostcode : (object)DBNull.Value),
                new SqlParameter("CompanyState", !string.IsNullOrEmpty(model.CompanyAddressProvinceState) ? model.CompanyAddressProvinceState : (object)DBNull.Value),
                new SqlParameter("CompanyCountry", !string.IsNullOrEmpty(model.CompanyAddressCountryCode) ? model.CompanyAddressCountryCode : (object)DBNull.Value),
                new SqlParameter("CreatedByID", CurrentUserID),
                new SqlParameter("ModifiedByID", CurrentUserID),
                new SqlParameter("IsActive", true));

            db.SaveChanges();
        }

        private string ReplaceAllToHash(string txt)
        {
            if (string.IsNullOrWhiteSpace(txt))
            {
                return "Empty Text...";
            }

            string _output = "";
            for (int i = 0; i < txt.Length; i++)
            {
                _output += "#";
            }

            return _output;
        }

        private int CurrentUserID
        {
            get
            {
                int userid = 0;
                int.TryParse(User.Identity.Name, out userid);
                return userid;
            }
        }

        [HttpPost]
        public ActionResult newsSubcribe(string email, string url)
        {
            string getText = string.Empty;
            String result_string = CommonServiceController.SaveEmail(email);

            if (result_string == "0")
            {
                Hashtable ht = new Hashtable();
                CommonServiceController.SendEmail(email, "You have subscribed to Mayflower’s Newsletter Successfully!", Core.getMailTemplate("subscription", ht));
                Session["newsletter_msg"] = "Thanks for subcription.";
            }
            else
            {
                Session["newsletter_msg"] = "Email already exist.";
            }

            return Redirect(url);
        }
    }
}
