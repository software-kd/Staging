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
using Alphareds.Module.BookingController;
using Alphareds.Module.MemberController;
using Alphareds.Module.CommonController;
using Alphareds.Module.Cryptography;
using Newtonsoft.Json;
using Alphareds.Module.Common;
using Microsoft.Ajax.Utilities;

namespace Mayflower.Controllers
{
    [Authorize]
    public class ToolsController : AsyncController
    {

        private Logger logger = LogManager.GetCurrentClassLogger();
        private MayFlower db = new MayFlower();
        public enum StatusMessage
        {
            success,
            error,
            fail
        }

        //not here move to booking services controller

        // GET: TravellerGroup

        public ActionResult TravellerGroup(int? page)
        {
            return Request.IsAjaxRequest()
              ? (ActionResult)this._TravellerGroup(page)
              : View();
        }

        public ActionResult _TravellerGroup(int? page)
        {
            IQueryable<TravellerGroupInsert> model = null;
            
            model = GetAllByID(CurrentUserID).OrderBy(m=>m.GroupName);
            model = model.Where(m => m.CreatedByID == CurrentUserID);


            int pageNumber = (page ?? 1);
            int pageSize = 10;

            var pagedModel = model.ToPagedList(pageNumber, pageSize);
 

            return PartialView("_TravellerGroup", pagedModel);
        }

        [HttpGet]
        public ActionResult CreateTraveller()
        {
            return Request.IsAjaxRequest()
              ? (ActionResult)this._CreateTraveller()
              : View();
        }

        [HttpPost]
        public ActionResult CreateTraveller(TravellerGroupInsert travellerGroupInsert)
        {
            try
            {

                foreach(var name in db.GroupNames.Where(x => x.CreatedByID == CurrentUserID))
                {
                    if(travellerGroupInsert.GroupName == name.GroupName1)
                    {
                        travellerGroupInsert.TravellerList = db.TravellerLists.Where(s => s.CreatedByID == CurrentUserID && s.IsActive).ToList();

                        ModelState.AddModelError("GroupName", "GroupName ''" + travellerGroupInsert.GroupName + "'' already Exists.");
                        return View(travellerGroupInsert);
                    }
                }
                if(travellerGroupInsert.SelectedGrp == null)
                {
                    travellerGroupInsert.TravellerList = db.TravellerLists.Where(s => s.CreatedByID == CurrentUserID && s.IsActive).ToList();

                    ModelState.AddModelError("Error", "No frequent traveller choosed");
                    return View(travellerGroupInsert);
                }


                SqlCommand command = new SqlCommand();
                GroupName groupName = new GroupName();
                groupName.GroupName1 = travellerGroupInsert.GroupName;
                groupName.CreatedByID = CurrentUserID;
                groupName.ModifiedByID = CurrentUserID;
                groupName.IsActive = true;

                MemberServiceController.InsertGroupName(groupName, command);
                command.Transaction.Commit();
                SqlCommand command2 = new SqlCommand();
                TravellerGroup travellerGroup = new TravellerGroup();

                foreach (var find in db.GroupNames)
                {
                    if (find.GroupName1 == travellerGroupInsert.GroupName && find.CreatedByID == CurrentUserID && find.IsActive == true)
                    {
                        travellerGroup.GroupID = find.GroupID;
                    }
                }
                travellerGroup.CreatedByID = CurrentUserID;
                travellerGroup.ModifiedByID = CurrentUserID;
                travellerGroup.IsActive = true;

                foreach (var data in travellerGroupInsert.SelectedGrp)
                {
                    travellerGroup.TravellerID = data;
                    MemberServiceController.InsertTravellerGroup(travellerGroup, command2);


                }
                command2.Transaction.Commit();
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Error when create new Traveller Group. - " + DateTime.Today.ToLoggerDateTime());
            }

            return RedirectToAction("TravellerGroup", "Tools");
        }

        public ActionResult _CreateTraveller()
        {
            IQueryable<FrequentFlyerViewModel> model = null;
            model = GetFlyerByUserID(CurrentUserID);
            model = model.OrderBy(x => x.TravellerID);
            return PartialView("_CreateTraveller", model);
        }
        
        [HttpGet]
        public ActionResult EditTraveller(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return RedirectToAction("NotFound", "Error");
            }

            //string _decID = "";
            //Cryptography.AES.TryDecrypt(name, out _decID);
            //int _grpId = _decID.ToInt();

            if (name != null)
            {
                var modelCaller = new ModelInitialize(db);
                var model = modelCaller.EditTraveller(CurrentUserID).FirstOrDefault(x => x.GroupName == name && x.CreatedByID == CurrentUserID);

                
                if (model == null)
                {
                    return HttpNotFound();
                }

                return View(model);
                //  return Request.IsAjaxRequest()
                //? (ActionResult)this._CreateTraveller()
                //: View(model);
            }
            else
            {
                return RedirectToAction("NotFound", "Error");
            }
        }

        [HttpPost]
        public ActionResult EditTraveller(TravellerGroupEditModel model)
        {
            SqlCommand command = new SqlCommand();
            model.IsActiveGroupName = true;
            MayFlower db = new MayFlower();
            bool nameIsAvailable = true;
            var groupNameList = db.GroupNames.Where(x => x.CreatedByID == CurrentUserID);
            var currentGroupName = groupNameList.FirstOrDefault(x => x.GroupID == model.GroupID);

            #region check/edit group name
            //if name is different
            if (model.GroupName != currentGroupName.GroupName1)
            {
                foreach (var item in groupNameList)
                {
                    if (item.GroupName1 == model.GroupName)
                    {
                        nameIsAvailable = false;
                    }
                }
                if (nameIsAvailable)
                {
                    //edit name
                    MemberServiceController.EditGroupName(CurrentUserID, model, command);
                    command.Transaction.Commit();
                }
                else
                {
                    //return error
                    model.FullTravellerList = db.TravellerLists.Where(s => s.CreatedByID == CurrentUserID && s.IsActive).ToList();
                    var tList = new List<TravellerList>();
                    if (model.SelectedGrp != null)
                    {
                        foreach (int selGroup in model.SelectedGrp)
                        {
                            var checkList = model.FullTravellerList.FirstOrDefault(x => x.TravellerID == selGroup);
                            if (checkList != null)
                            {
                                tList.Add(checkList);
                            }
                        }
                        model.TravellerList = tList;
                        model.SelectedGrp.Clear();
                    }

                    ModelState.AddModelError("GroupName", "GroupName ''" + model.GroupName + "'' Already Exist.");
                    return View(model);
                }
            }
            #endregion

            #region old edit group name
            //foreach (var item in db.GroupNames)
            //{


            //    if (item.GroupID == model.GroupID && item.CreatedByID == CurrentUserID)
            //    {
            //        MemberServiceController.EditGroupName(CurrentUserID, model, command);

            //        command.Transaction.Commit();
            //    }
            //    else if (item.GroupID != model.GroupID && item.GroupName1 == model.GroupName && item.CreatedByID == CurrentUserID)
            //    {
            //        //when groupname exist at other travellergroup for same userID
            //        model.FullTravellerList = db.TravellerLists.Where(s => s.CreatedByID == CurrentUserID && s.IsActive).ToList();
            //        var tList = new List<TravellerList>();
            //        if (model.SelectedGrp != null)
            //        {
            //            foreach (int selGroup in model.SelectedGrp)
            //            {
            //                var checkList = model.FullTravellerList.FirstOrDefault(x => x.TravellerID == selGroup);
            //                if (checkList != null)
            //                {
            //                    tList.Add(checkList);
            //                }
            //            }
            //            model.TravellerList = tList;
            //        }

            //        model.SelectedGrp.Clear();
            //        ModelState.AddModelError("Error", "GroupName ''" + model.GroupName + "'' Already Exist.");
            //        return View(model);
            //        //throw new Exception("GroupName Exist");
            //    }
            //}
            #endregion

            var selectedTG = db.TravellerGroups.Where(x => x.GroupID == model.GroupID && x.CreatedByID == CurrentUserID);
            foreach (var tg in selectedTG)//choosed
            {
                bool checkDB = false;
                if(model.SelectedGrp !=null)
                {
                    foreach (var ti in model.SelectedGrp) //check equal
                    {
                        
                        if(ti == tg.TravellerID)
                        {
                            
                            SqlCommand command2 = new SqlCommand();
                            model.ModifiedByID = CurrentUserID;
                            model.TravellerGroupID = tg.TravellerGroupID;
                            model.TravellerID = ti;
                            model.IsActiveTravellerName = true;
                            MemberServiceController.EditTravellerGroup(CurrentUserID, model, command2);

                            command2.Transaction.Commit();
                            checkDB = true;
                        
                        }
                        

                    }
                }
                else
                {
                    // when no frequent traveller chosen when edit
                    model.FullTravellerList = db.TravellerLists.Where(s => s.CreatedByID == CurrentUserID && s.IsActive).ToList();
                    var tList = new List<TravellerList>();
                    if (model.SelectedGrp == null)
                    {
                        //model.SelectedGrp.Clear();
                        ModelState.AddModelError("Error", "No frequent traveller choosed");

                        return View(model);
                    }
                    //}
                    //foreach (int selGroup in model.SelectedGrp)
                    //{
                    //    var checkList = model.FullTravellerList.FirstOrDefault(x => x.TravellerID == selGroup);
                    //    if (checkList != null)
                    //    {
                    //        tList.Add(checkList);
                    //    }
                    //}
                    model.TravellerList = tList;
                    model.SelectedGrp.Clear();
                    ModelState.AddModelError("Error", "No frequent traveller choosed");
                    return View(model);


                    //throw new Exception("No frequent traveller choosed");
                }

                if (!checkDB)
                {
                    SqlCommand command2 = new SqlCommand();
                    model.ModifiedByID = CurrentUserID;
                    model.TravellerGroupID = tg.TravellerGroupID;
                    model.TravellerID = tg.TravellerID;
                    model.IsActiveTravellerName = false;
                    MemberServiceController.EditTravellerGroup(CurrentUserID, model, command2);

                    command2.Transaction.Commit();
                }    
            }
            //
            foreach(var list in model.SelectedGrp)
            {
                bool checkModel = false;
                foreach(var tg in selectedTG)
                {
                    if(list == tg.TravellerID)
                    {
                        checkModel = true;
                    }
                }
                if(!checkModel)
                {
                    SqlCommand command3 = new SqlCommand();
                    model.TravellerID = list;
                    model.CreatedByID = CurrentUserID;
                    model.ModifiedByID = CurrentUserID;
                    model.IsActiveTravellerName = true;
                    MemberServiceController.InsertTravellerGroupByEdit(model, command3);
                    command3.Transaction.Commit();
                }
            }
            return RedirectToAction("TravellerGroup", "Tools");
        }
        
        public ActionResult DeleteTraveller(string name)
        {
            if (name != null)
            {
                var modelCaller = new ModelInitialize(db);
                var model = modelCaller.DeleteAll(CurrentUserID).FirstOrDefault(x => x.GroupName == name);
                foreach (var item in model.TravallerGroup)
                {
                    SqlCommand command2 = new SqlCommand();
                    model.TravellerGroupID = item.TravellerGroupID;
                    MemberServiceController.DeleteTravellerGroup(model, command2);
                    command2.Transaction.Commit();
                }
                SqlCommand command = new SqlCommand();
                MemberServiceController.DeleteGroupName(model, command);
                command.Transaction.Commit();


                

                if (model == null)
                {
                    return HttpNotFound();
                }

            }
            return RedirectToAction("TravellerGroup","Tools");
        }

        [HttpGet]
        public ActionResult NewFrequentFlyer()
        {

            return View();
        }

        [HttpPost]
        public ActionResult NewFrequentFlyer(FrequentFlyerInsert frequentFlyerInsert)
        {
            SqlCommand command = new SqlCommand();
            Alphareds.Module.Model.Database.TravellerList travellerList = new Alphareds.Module.Model.Database.TravellerList();
            travellerList.TitleCode = frequentFlyerInsert.FlyerDetail.TitleCode;
            travellerList.FirstName = frequentFlyerInsert.FlyerDetail.FirstName;
            travellerList.FamilyName = frequentFlyerInsert.FlyerDetail.FamilyName;
            travellerList.DOB = frequentFlyerInsert.FlyerDetail.DOB ?? DateTime.Now;
            travellerList.Nationality = frequentFlyerInsert.FlyerDetail.Nationality;
            travellerList.Passport = frequentFlyerInsert.FlyerDetail.Passport;
            travellerList.PassportIssuePlace = frequentFlyerInsert.FlyerDetail.PassportIssuePlace;
            travellerList.PassportExpiryDate = frequentFlyerInsert.FlyerDetail.PassportExpiryDate ?? DateTime.MaxValue;
            travellerList.IsShared = frequentFlyerInsert.FlyerDetail.IsShared;
            travellerList.CreatedByID = CurrentUserID;
            travellerList.ModifiedByID = CurrentUserID;
            travellerList.IsActive = true;

            MemberServiceController.InsertFrequentFlyer(travellerList, command);
            command.Transaction.Commit();
            return RedirectToAction("FrequentFlyer", "Tools");
        }

        [HttpGet]
        public ActionResult FrequentFlyer(int? page, StatusMessage? message, string role)
        {
            if(TempData["ModelStateDeleteError"] != null)
            {
                ModelState.Merge((ModelStateDictionary)TempData["ModelStateDeleteError"]);
            }
            return Request.IsAjaxRequest()
               ? (ActionResult)this._FlyerList(page, message, role)
               : View();
        }

        public ActionResult _FlyerList(int? page, StatusMessage? message, string role = null)
        {
            #region Action Return Message
            if (message == StatusMessage.error)
            { ViewBag.StatusMessage = MvcHtmlString.Create("Unexpected error, please contact our us for help."); }
            else
            { ViewBag.StatusMessage = null; }
            #endregion

            User user = db.Users.FirstOrDefault(x => x.UserID == CurrentUserID);
            role = user.UserTypeCode;

            #region Model Initialize
            IQueryable<FrequentFlyerViewModel> model = null;

            if (role == "System Admin")
            {
                model = GetBookAll();
            }
            else if(role =="AGT")
            {
                model = GetFlyerBySharedAgent(user.OrganizationID,CurrentUserID);
            }
            else
            {
                model = GetFlyerByUserID(CurrentUserID).Where(m=>m.IsActive==true);
            }
            #endregion

            model = model.OrderBy(x => x.FullName);

            // Use IPagedModel to fix performance issues.
            int pageNumber = (page ?? 1);
            int pageSize = 10;

            var pagedModel = model.ToPagedList(pageNumber, pageSize);

            return PartialView("_FlyerList", pagedModel);
        }

        public static IQueryable<FrequentFlyerViewModel> GetBookAll()
        {
            MayFlower db = new MayFlower();
            var FlyerModel = from ff in db.TravellerLists
                             from user in db.Users
                             where user.UserID == ff.CreatedByID
                             select new FrequentFlyerViewModel
                             {
                                 FullName = ff.FirstName + "/" + ff.FamilyName,
                                 UserID = ff.CreatedByID,
                                 IsShared = ff.IsShared,
                                 TravellerID = ff.TravellerID,
                                 IsActive = ff.IsActive,
                                 OrganizationID = user.OrganizationID,
                                 
                             };
            return FlyerModel.AsQueryable();
        }

        public static IQueryable<TravellerGroupInsert> GetAllByID(int userId)
        {
            MayFlower db = new MayFlower();
            var model = db.GroupNames
                    .Where(x => x.IsActive)
                    .Select(x =>
                new TravellerGroupInsert
                {
                    GroupID = x.GroupID,
                    GroupName = x.GroupName1,
                    CreatedByID = x.CreatedByID,
                    TravellerList = db.TravellerGroups.Where(s => s.GroupID == x.GroupID && s.TravellerList.IsActive && s.IsActive)
                    .Select(s => s.TravellerList).ToList()
                });

            return model;
        }

        public static IQueryable<FrequentFlyerViewModel> GetFlyerByUserID(int UserId)
        {
            var FlyerModel = GetBookAll().Where(x => x.UserID == UserId);
            return FlyerModel;
        }

        public static IQueryable<FrequentFlyerViewModel> GetFlyerBySharedAgent(int organizationId,int userId)
        {
            var FlyerModel = GetBookAll().Where(x => x.OrganizationID == organizationId &&x.IsShared==true || x.UserID==userId);
            return FlyerModel;
        }
        


        [HttpGet]
        public ActionResult EditFrequentFlyer(string id)
        {
            var modelCaller = new ModelInitialize(db);
            var model = modelCaller.EditFlyerDetail().FirstOrDefault(x => x.TravellerID.ToString() == id);

            if (model == null)
            {
                return HttpNotFound();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditFrequentFlyer(int id, FrequentFlyerEditModel model)
        {
            SqlCommand command = new SqlCommand();

            model.IsActive = true;
            MemberServiceController.EditFrequentFlyer(id, CurrentUserID, model, command);
            command.Transaction.Commit();
            return RedirectToAction("FrequentFlyer", "Tools");
        }

        [HttpGet]
        public ActionResult _DeleteFlyer(string id)
        {
            var modelCaller = new ModelInitialize(db);
            var model = modelCaller.EditFlyerDetail().FirstOrDefault(x => x.TravellerID.ToString() == id);

            if (model == null)
            {
                return HttpNotFound();
            }

            return PartialView("_DeleteFlyer",model);
        }

        public ActionResult DeleteFrequentFlyer(int id)
        {
            var modelCaller = new ModelInitialize(db);
            var model = modelCaller.EditFlyerDetail().FirstOrDefault(x => x.TravellerID == id);
            MayFlower checkDb = new MayFlower();
            foreach(var group in checkDb.TravellerGroups.Where(x => x.IsActive == true))
            {
                if(group.TravellerID == model.TravellerID)
                {
                    ModelState.AddModelError("Error", "This frequent traveller " + model.FullName + " added in Traveller Group!");
                    TempData["ModelStateDeleteError"] = ModelState;
                    return RedirectToAction("FrequentFlyer", "Tools", ModelState);
                    //throw new Exception("This frequent traveller added in traveller group!");
                }

            }
            model.IsActive = false;

            SqlCommand command = new SqlCommand();
            MemberServiceController.EditFrequentFlyer(id, CurrentUserID, model, command);
            command.Transaction.Commit();

            if (model == null)
            {
                return HttpNotFound();
            }
            return RedirectToAction("FrequentFlyer", "Tools");
        }

        public class ModelInitialize
        {
            private MayFlower db = new MayFlower();

            public ModelInitialize(MayFlower dbContext)
            {
                db = dbContext;
            }

            public IQueryable<FrequentFlyerEditModel> EditFlyerDetail()
            {
                MayFlower db = new MayFlower();
                var model = from gf in db.TravellerLists
                            select new FrequentFlyerEditModel
                            {
                                TravellerID = gf.TravellerID,
                                TitleCode = gf.TitleCode,
                                FirstName = gf.FirstName,
                                FamilyName = gf.FamilyName,
                                DOB = gf.DOB,
                                Nationality = gf.Nationality,
                                Passport = gf.Passport,
                                PassportExpiryDate = gf.PassportExpiryDate,
                                PassportIssuePlace = gf.PassportIssuePlace,
                                IsShared = gf.IsShared ?? false,

                            };

                return model;
            }

            public IQueryable<DeleteTravellerGroupModel> DeleteAll(int userId)
            {
                MayFlower db = new MayFlower();
                var model = db.GroupNames.Select(m => new DeleteTravellerGroupModel
                {
                    GroupID = m.GroupID,
                    GroupName = m.GroupName1,

                    TravallerGroup = db.TravellerGroups.Where(s => s.GroupID == m.GroupID && s.CreatedByID == userId).ToList()
                });
                return model;

            }

            public IQueryable<TravellerGroupEditModel> EditTraveller(int userId)
            {
                MayFlower db = new MayFlower();

                var travellerModel = db.GroupNames
                    .Where(x => x.IsActive)
                    .Select(x =>
                new TravellerGroupEditModel
                {
                    GroupID = x.GroupID,
                    GroupName = x.GroupName1,
                    CreatedByID = x.CreatedByID,
                    
                    TravellerList = db.TravellerGroups.Where(s => s.GroupID == x.GroupID && s.TravellerList.IsActive && s.IsActive)
                    .Select(s => s.TravellerList).ToList(),
                    FullTravellerList = db.TravellerLists.Where(s => s.CreatedByID == userId && s.IsActive).ToList()
                });
                return travellerModel;
            }

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
    }

}