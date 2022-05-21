using System.IO;
using System.Threading.Tasks;
using API.Errors;
using Core.Entities;
using Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stripe;
using Order = Core.Entities.OrderAggregate.Order;

namespace API.Controllers
{
    public class PaymentsController : BaseApiController
    {
        private readonly IPaymentService _paymentService;
        private const string WhSecret = "whsec_8a9aa0a06f02b9be38389a70720068b2d63633b08e76fbf14c65849d4787c99e";
        private readonly ILogger<PaymentsController> _logger;
        public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
        {
            _logger = logger;
            _paymentService = paymentService;

        }

        [Authorize]
        [HttpPost("{basketId}")]
        public async Task<ActionResult<CustomerBasket>> CreateOrUpdatePaymentIntent(string basketId)
        {
            var basket = await _paymentService.CreateOrUpdatePaymentIntent(basketId);

            if (basket == null) return BadRequest(new ApiResponse(400, "Problem with your basket"));
            return basket;
        }

        [HttpPost("webhook")]

        public async Task<ActionResult> StripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            var stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], WhSecret);

            PaymentIntent intent;
            Order order;

            switch (stripeEvent.Type)
            {
                case "payment_intent.succeeded":
                    intent = (PaymentIntent)stripeEvent.Data.Object;
                    _logger.LogInformation("Payment Succeeded: ", intent.Id);
                    order = await _paymentService.UpdateOrderPaymentSucceeded(intent.Id);
                    _logger.LogInformation("Order updated to payment received: ", order.Id);
                    break;
                case "Payment_intent.payment_failed":
                    intent = (PaymentIntent)stripeEvent.Data.Object;
                    _logger.LogInformation("Payment Failed: ", intent.Id);
                    order = await _paymentService.UpdateOrderPaymentFailed(intent.Id);
                    _logger.LogInformation("payment Failed: ", order.Id);
                    break;
            }

            return new EmptyResult();
        }
    }
}