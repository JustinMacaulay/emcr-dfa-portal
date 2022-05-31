﻿using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EMBC.Tests.Automation.Responders.PageObjects
{
    public class ResponderManagement : PageObjectBase
    {
        private By responderMgmtTab = By.XPath("//mat-sidenav-content[1]/app-top-nav-menu[1]/mat-toolbar[1]/div[1]/a[3]");
        private By addTeamMemberTab = By.CssSelector("div[class='mat-tab-links'] a:nth-child(2)");
        private By addTeamMemberFirstNameInput = By.CssSelector("input[formcontrolname='firstName']");
        private By addTeamMemberLastNameInput = By.CssSelector("input[formcontrolname='lastName']");
        private By addTeamMemberUsernameInput = By.CssSelector("input[formcontrolname='userName']");

        private By searchTeamMemberInput = By.Id("searchInput");
        private By respondersManagementMainTableRows = By.CssSelector("table[role='table'] tbody tr");
        private By respondersManagementMainTableFirstRows = By.CssSelector("table[role='table'] tbody tr:nth-child(1)");
        private By respondersManagementMainTableFirstRowStatus = By.CssSelector("table[role='table'] tbody tr:nth-child(1) td:nth-child(6)");
        private By respondersManagementStatusToggle = By.TagName("mat-slide-toggle");

        private By deleteTeamMemberCheckBox = By.Id("confirmDelete");


        public ResponderManagement(IWebDriver webDriver) : base(webDriver)
        { }

        public void EnterResponderManagement()
        {
            webDriver.FindElement(responderMgmtTab).Click();
        }

        public void AddNewTeamMemberTab()
        {
            webDriver.FindElement(addTeamMemberTab).Click();
        }

        public void AddNewTeamMember(string firstName, string lastName, string userName)
        {
            Wait();
            webDriver.FindElement(addTeamMemberFirstNameInput).SendKeys(firstName);
            webDriver.FindElement(addTeamMemberLastNameInput).SendKeys(lastName);
            webDriver.FindElement(addTeamMemberUsernameInput).SendKeys(userName);
            ButtonElement("Next");

            Wait();
            ButtonElement("Save");
            Wait();
            ButtonElement("Close");

        }

        public void SearchTeamMember(string searchInput)
        {
            webDriver.FindElement(searchTeamMemberInput).Clear();
            webDriver.FindElement(searchTeamMemberInput).SendKeys(searchInput);
            ButtonElement("Search");

        }

        public void ChangeTeamMemberStatus()
        {
            Wait();
            webDriver.FindElement(respondersManagementStatusToggle).Click();
        }

        public void SelectTeamMember()
        {
            Wait();
            webDriver.FindElement(respondersManagementMainTableFirstRows).Click();
        }

        public void DeleteTeamMember()
        {
            Wait();
            ButtonElement("Delete User");
            FocusAndClick(deleteTeamMemberCheckBox);
            ButtonElement("Yes, Delete this User");

            Wait();
            ButtonElement("Close");
        }

        // ASSERT FUNCTIONS

        public string GetMemberStatus()
        {
            Wait();
            return webDriver.FindElement(respondersManagementMainTableFirstRowStatus).Text;
        }

        public int GetTotalSearchNumber()
        {
            Wait();
            return webDriver.FindElements(respondersManagementMainTableRows).Count;

        }
    }
}
