using Finerr.BusinessLogic;
using Finerr.Constants;
using Finerr.DataEntities.ViewModel;
using Finerr.Models;
using Finerr.Utility.CommonMethod;
using MagnusMinds.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;

namespace Finerr.WebApp.Controllers
{
    public class StripeController : Controller
    {
        private readonly IConfiguration _config;
        private readonly UnitofWorkBL _unitofWork;


        public StripeController(IConfiguration config,UnitofWorkBL unitofWorkBL)
        {
            _config = config;
            _unitofWork = unitofWorkBL;
        }

        public IActionResult Index(string Id)
        {
            if (!string.IsNullOrEmpty(Id))
            {
                string encId = Id;
                Id = Id.Replace(" ", "+");
                Id = Id.Replace("-", "/");

                bool validateId = CommonMethod.IsBase64String(Id);
                if (validateId)
                {
                    Id = Encryption.Decrypt(Id, true, _config["Encrypt:EncryptKey"]);
                    Stripe.StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
                    ViewBag.stripeKey = _config["Stripe:PublishableKey"];
                    var getPharmacyDetails = _unitofWork.CustomerPaymentBL.GetPaymentDetailsById(Convert.ToInt32(Id));
                    ViewBag.PlanName = getPharmacyDetails.PlanName;
                    ViewBag.Price = getPharmacyDetails.Price;
                    if (getPharmacyDetails.DiscountedPrice != null)
                    {
                        ViewBag.DiscountedPrice = getPharmacyDetails.DiscountedPrice;
                        ViewBag.TotalDiscountedPrice = Convert.ToDecimal(getPharmacyDetails.Price) - Convert.ToDecimal(getPharmacyDetails.DiscountedPrice);
                    }
                    else
                        ViewBag.DiscountedPrice = getPharmacyDetails.Price;

                    CardVM model = new CardVM();
                    model.Id = encId;
                    return View(model);
                }
                else
                    return RedirectToAction("OnboardingCustomer", new { id = 0 });
            }
            else
                return RedirectToAction("OnboardingCustomer", new { id = 0 });
        }

        public IActionResult CheckOut()
        {
            List<StripeProductEntity> products = new List<StripeProductEntity>
            {
                new StripeProductEntity
                {
                    Product = "Finerr",
                    Quantity = 1,
                    Rate = 100,
                }
            };
            var domain = "https://localhost:44397/";
            var options = new SessionCreateOptions
            {
                SuccessUrl = domain + $"CheckOut/OrderConfirmation",
                CancelUrl = domain + $"CheckOut/Login",
                LineItems = new List<SessionLineItemOptions>(),
                Mode = "subscription",
            };

            foreach (var item in products)
            {
                var sessionLineItems = new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(item.Rate * item.Quantity),
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Product.ToString(),
                        }
                    },
                    Quantity = item.Quantity,
                };
                options.LineItems.Add(sessionLineItems);
            }
            var service = new SessionService();
            Stripe.StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
            Session session = service.Create(options);
            Response.Headers.Add("Location", session.Url);

            return new StatusCodeResult(303);
        }

        public IActionResult Subscription()
        {
            var domain = "https://app.finerr.com/";
            //var priceOptions = new PriceListOptions
            //{
            //    LookupKeys = new List<string> {
            //       "price_1NrdoXHjx2cLxjhxAraAIiq3"
            //    }
            //};
            //var priceService = new PriceService();
            Stripe.StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
            //StripeList<Price> prices = priceService.List(priceOptions);

            var options = new SessionCreateOptions
            {
                LineItems = new List<SessionLineItemOptions>
                {
                  new SessionLineItemOptions
                  {
                    Price = "price_Id",
                    Quantity = 1,
                  },
                },
                Mode = "subscription",
                SuccessUrl = domain + $"CheckOut/OrderConfirmation",
                CancelUrl = domain + $"CheckOut/Login",
            };
            var service = new SessionService();
            Session session = service.Create(options);

            Response.Headers.Add("Location", session.Url);
            return new StatusCodeResult(303);
        }

        /// <summary>
        /// Subscribe
        /// </summary>
        /// <param name="email"></param>
        /// <param name="plan"></param>
        /// <param name="stripeToken"></param>
        /// <returns></returns>
        [HttpPost]
        [Authorize(PermissionsVM.Subscription.Create)]
        [ValidateAntiForgeryToken]
        public ActionResult Subscribe(string stripeToken, string Id)
        {
            try
            {
                Stripe.StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];

                string planId = "price_Id";
                var customerOptions = new CustomerCreateOptions
                {
                    Email = "sharvilb@gmail.com",
                    Source = stripeToken,
                    Name = "sharvil",
                };

                var customerService = new CustomerService();
                var customer = customerService.Create(customerOptions);

                var subscriptionOptions = new SubscriptionCreateOptions
                {
                    Customer = customer.Id,
                    Items = new List<SubscriptionItemOptions>
                    {
                        new SubscriptionItemOptions
                        {
                            Plan = planId,
                        },
                    },
                };
                subscriptionOptions.AddExpand("latest_invoice.payment_intent");

                var subscriptionService = new SubscriptionService();
                var subscription = subscriptionService.Create(subscriptionOptions);

                ViewBag.stripeKey = _config["Stripe:PublishableKey"];
                ViewBag.subscription = subscription.ToJson();
                return View();
            }
            catch (Exception ex)
            {
                return RedirectToAction("PaymentFailed");
            }
        }
    }
}