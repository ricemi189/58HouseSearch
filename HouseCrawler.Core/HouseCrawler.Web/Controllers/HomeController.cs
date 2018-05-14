﻿using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using HouseCrawler.Web.Models;
using HouseCrawler.Web.Common;
using HouseCrawler.Web;
using HouseCrawler.Web.DataContent;
using HouseCrawler.Web.Service;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Collections.Generic;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace HouseCrawler.Web.Controllers
{
    public class HomeController : Controller
    {
        private CrawlerDataContent dataContent = new CrawlerDataContent();

        // GET: /<controller>/
        public IActionResult Index()
        {
            var idAndName = GetUserIDAndName();
            ViewBag.UserId = idAndName.Item1;
            ViewBag.UserName = idAndName.Item2;
            var dashboards = HouseDashboardService.LoadDashboard();
            return View(dashboards);
        }

        public IActionResult HouseList()
        {
            return View();
        }

        public IActionResult GetHouseInfo(string cityName, string source = "", int houseCount = 100,
            int intervalDay = 7, string keyword = "", bool refresh = false)
        {
            try
            {
                var houseList = HouseDapper.SearchHouseInfo(cityName, source, houseCount, intervalDay, keyword, refresh);
                var rooms = houseList.Select(house =>
                {
                    var markBGType = string.Empty;
                    int housePrice = (int)house.HousePrice;
                    if (housePrice > 0)
                    {
                        markBGType = LocationMarkBGType.SelectColor(housePrice / 1000);
                    }

                    return new HouseInfo
                    {
                        ID = house.Id,
                        Source = house.Source,
                        Money = house.DisPlayPrice,
                        HouseURL = house.HouseOnlineURL,
                        HouseLocation = house.HouseLocation,
                        HouseTime = house.PubTime.ToString(),
                        HouseTitle = house.HouseTitle,
                        HousePrice = housePrice,
                        LocationMarkBG = markBGType,
                        DisplaySource = ConstConfigurationName.ConvertToDisPlayName(house.Source)
                    };
                });
                return Json(new { IsSuccess = true, HouseInfos = rooms });
            }
            catch (Exception ex)
            {
                return Json(new { IsSuccess = false, error = ex.ToString() });
            }
        }


        public IActionResult AddDouBanGroup(string doubanGroup, string cityName)
        {
            if (string.IsNullOrEmpty(doubanGroup) || string.IsNullOrEmpty(cityName))
            {
                return Json(new { IsSuccess = false, error = "请输入豆瓣小组Group和城市名称。" });
            }
            var topics = DoubanHouseCrawler.GetHouseData(doubanGroup, cityName, 1);
            if (topics != null && topics.Count() > 0)
            {
                var cityInfo = $"{{ 'groupid':'{doubanGroup}','cityname':'{cityName}','pagecount':5}}";
                var doubanConfig = dataContent.CrawlerConfigurations
                    .FirstOrDefault(c => c.ConfigurationName == ConstConfigurationName.Douban && c.ConfigurationValue == cityInfo);
                if (doubanConfig != null)
                {
                    return Json(new { IsSuccess = true });
                }
                var config = new BizCrawlerConfiguration()
                {
                    ConfigurationKey = 0,
                    ConfigurationValue = cityInfo,
                    ConfigurationName = ConstConfigurationName.Douban,
                    DataCreateTime = DateTime.Now,
                    IsEnabled = true,
                };
                dataContent.Add(config);
                dataContent.SaveChanges();
                return Json(new { IsSuccess = true });
            }
            else
            {
                return Json(new
                {
                    IsSuccess = false,
                    error = "保存失败!请检查豆瓣小组ID（如：XMhouse）/城市名称（如：厦门）是否正确..."
                });
            }



        }


        public IActionResult AddUserCollection(long houseId, string source)
        {
            var user = ((ClaimsIdentity)HttpContext.User.Identity);
            if (user == null || user.FindFirst(ClaimTypes.NameIdentifier) == null)
            {
                return Json(new { successs = false, error = "请登录后再收藏房源." });
            }
            var userId = user.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (string.IsNullOrEmpty(userId) || userId == "0")
            {
                return Json(new { successs = false, error = "请登录后再收藏房源." });
            }
            var house = HouseDapper.GetHouseID(houseId, source);
            if (house == null)
            {
                return Json(new { successs = false, error = "房源信息不存在,请刷新页面后重试." });
            }
            var userCollection = new UserCollection();
            userCollection.UserID = long.Parse(userId);
            userCollection.HouseID = houseId;
            userCollection.Source = house.Source;
            userCollection.HouseCity = house.LocationCityName;
            UserCollectionDapper.InsertUser(userCollection);
            return Json(new { success = true, message = "收藏成功." }); ;
        }


        public IActionResult GetUserCollectionHouses(string cityName, string source = "")
        {
            try
            {
                var userID = GetUserID();
                if (userID == 0)
                {
                    return Json(new { IsSuccess = false, error = "用户未登陆，无法查看房源收藏。" });
                }
                var houseList = UserCollectionDapper.FindUserCollections(userID, cityName, source);
                var rooms = houseList.Select(house =>
                {
                    var markBGType = string.Empty;
                    int housePrice = (int)house.HousePrice;
                    if (housePrice > 0)
                    {
                        markBGType = LocationMarkBGType.SelectColor(housePrice / 1000);
                    }
                    return new HouseInfo
                    {
                        ID = house.Id,
                        Source = house.Source,
                        Money = house.DisPlayPrice,
                        HouseURL = house.HouseOnlineURL,
                        HouseLocation = house.HouseLocation,
                        HouseTime = house.PubTime.ToString(),
                        HouseTitle = house.HouseTitle,
                        HousePrice = housePrice,
                        LocationMarkBG = markBGType,
                        DisplaySource = ConstConfigurationName.ConvertToDisPlayName(house.Source)
                    };
                });
                return Json(new { IsSuccess = true, HouseInfos = rooms });
            }
            catch (Exception ex)
            {
                return Json(new { IsSuccess = false, error = ex.ToString() });
            }
        }

        private long GetUserID()
        {
            var identity = ((ClaimsIdentity)HttpContext.User.Identity);
            if (identity == null || identity.FindFirst(ClaimTypes.NameIdentifier) == null)
            {
                return 0;
            }
            var userID = identity.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (string.IsNullOrEmpty(userID) || userID == "0")
            {
                return 0;
            }

            return long.Parse(userID);
        }

        public Tuple<long, string> GetUserIDAndName()
        {
            var identity = ((ClaimsIdentity)HttpContext.User.Identity);
            if (identity == null || identity.FindFirst(ClaimTypes.NameIdentifier) == null)
            {
                return Tuple.Create<long, string>(0, string.Empty);
            }
            var userID = identity.FindFirst(ClaimTypes.NameIdentifier).Value;
            var userName = identity.FindFirst(ClaimTypes.Name).Value;
            if (string.IsNullOrEmpty(userID) || userID == "0")
            {
                return Tuple.Create<long, string>(0, string.Empty);
            }
            return Tuple.Create<long, string>(long.Parse(userID), userName);
        }

        public IActionResult UserCollection()
        {
            return View();
        }

        public IActionResult UserHouseDashboard()
        {
            var houseDashboards = new List<HouseDashboard>();
            var userID = GetUserID();
            if (userID == 0)
            {
                return PartialView("UserHouseDashboard", houseDashboards);
            }
            houseDashboards = UserCollectionDapper.LoadUserHouseDashboard(userID);
            return PartialView("UserHouseDashboard", houseDashboards);
        }


        public IActionResult UserHouseList()
        {
            var userHouses = new List<DBHouseInfo>();
            var userID = GetUserID();
            if (userID == 0)
            {
                // userHouses.Add(new DBHouseInfo(){ 
                //     Id =-1,
                //     LocationCityName = "上海",
                //     HouseTitle ="测试房源",
                //     HouseText = "测试内容"
                //     });
                return PartialView("UserHouseList", userHouses);
            }
            userHouses = UserCollectionDapper.FindUserCollections(userID);
            return PartialView("UserHouseList", userHouses);
        }
    }
}
