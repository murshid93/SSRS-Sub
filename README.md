# SSRS Subscription API

A robust ASP.NET Core Web API designed to dynamically create, trigger, and monitor SQL Server Reporting Services (SSRS) subscriptions. 

This API acts as a lightweight middleware, allowing applications to trigger SSRS reports via a simple URL payload. It supports both **Email** and **File Share** delivery methods, and features an asynchronous background worker that polls the SSRS server to send Office 365 SMTP email notifications the moment a file is successfully saved to a network drive.

## Features
* **Dynamic URL Parsing:** Automatically extracts standard routing parameters and passes any custom dynamic parameters (like `Chain` or `Branch`) directly to the SSRS report.
* **Dual Delivery Methods:** Seamlessly route reports to either an Email inbox or a Network File Share.
* **Fire-and-Forget Architecture:** API responds instantly (zero timeouts), while a background thread monitors the SSRS rendering status.
* **Smart Notifications:** When using File Share delivery, providing an email address will automatically trigger a success notification containing the exact network path of the generated file.

## Configuration

Before running the application, ensure your `appsettings.json` is configured with your SSRS REST API endpoint, default file share paths, and Office 365 SMTP credentials:

```json
{
  "SsrsSettings": {
    "BaseUrl": "https://[YOUR_SERVER]/reports/api/v2.0",
    "Username": "service_account",
    "Password": "password",
    "Domain": "DOMAIN",
    "DefaultEmailTo": "fallback@domain.com",
    "DefaultRenderFormat": "PDF",
    "DefaultFileSharePath": "\\\\172.22.x.x\\ReportsQueueFileShare",
    "DefaultFileShareUsername": "DOMAIN\\service_account",
    "DefaultFileSharePassword": "password"
  },
  "SmtpSettings": {
    "Server": "smtp.office365.com",
    "Port": 587,
    "SenderEmail": "app_sender@domain.com",
    "Username": "app_sender@domain.com",
    "Password": "password"
  }
}
Endpoints
POST /api/send-report-from-url
Expects a JSON body containing the URL string to be parsed.
{
  "url": "https://..."
}

URL Parameters & Usage
The API uses a unified URL structure. Core parameters dictate the delivery behavior, while any unrecognized parameters are automatically treated as SSRS Report Parameters.
Parameter,Required,Description
report_path,Yes,The exact path to the report on the SSRS server.
delivery_method,No,Email or FileShare. Defaults to Email if omitted.
email_to,Conditional,"Required for Email delivery. For File Share, providing this triggers an SMTP notification upon completion."
file_name,No,Desired name for the output file (FileShare only).
subject,No,Email subject line.
comment,No,Body text for Email delivery.
Gemini said
Here is a clean, professional, and comprehensive README.md tailored specifically for your project. It covers the architecture, the setup, and the exact URL structures you need to pass into the API.

You can copy this directly into your GitHub repository!

Markdown
# SSRS Subscription API

A robust ASP.NET Core Web API designed to dynamically create, trigger, and monitor SQL Server Reporting Services (SSRS) subscriptions. 

This API acts as a lightweight middleware, allowing applications to trigger SSRS reports via a simple URL payload. It supports both **Email** and **File Share** delivery methods, and features an asynchronous background worker that polls the SSRS server to send Office 365 SMTP email notifications the moment a file is successfully saved to a network drive.

## Features
* **Dynamic URL Parsing:** Automatically extracts standard routing parameters and passes any custom dynamic parameters (like `Chain` or `Branch`) directly to the SSRS report.
* **Dual Delivery Methods:** Seamlessly route reports to either an Email inbox or a Network File Share.
* **Fire-and-Forget Architecture:** API responds instantly (zero timeouts), while a background thread monitors the SSRS rendering status.
* **Smart Notifications:** When using File Share delivery, providing an email address will automatically trigger a success notification containing the exact network path of the generated file.

## Configuration

Before running the application, ensure your `appsettings.json` is configured with your SSRS REST API endpoint, default file share paths, and Office 365 SMTP credentials:

```json
{
  "SsrsSettings": {
    "BaseUrl": "https://[YOUR_SERVER]/reports/api/v2.0",
    "Username": "service_account",
    "Password": "password",
    "Domain": "DOMAIN",
    "DefaultEmailTo": "fallback@domain.com",
    "DefaultRenderFormat": "PDF",
    "DefaultFileSharePath": "\\\\172.22.x.x\\ReportsQueueFileShare",
    "DefaultFileShareUsername": "DOMAIN\\service_account",
    "DefaultFileSharePassword": "password"
  },
  "SmtpSettings": {
    "Server": "smtp.office365.com",
    "Port": 587,
    "SenderEmail": "app_sender@domain.com",
    "Username": "app_sender@domain.com",
    "Password": "password"
  }
}
Endpoints
POST /api/send-report-from-url
Expects a JSON body containing the URL string to be parsed.

Request Body:

JSON
{
  "url": "https://..."
}
URL Parameters & Usage
The API uses a unified URL structure. Core parameters dictate the delivery behavior, while any unrecognized parameters are automatically treated as SSRS Report Parameters.

Parameter	Required	Description
report_path	Yes	        The exact path to the report on the SSRS server.
delivery_method	No	        Email or FileShare. Defaults to Email if omitted.
email_to	Conditional	Required for Email delivery. For File Share, providing this triggers an SMTP notification upon completion.
file_name	No	        Desired name for the output file (FileShare only).
subject	No	Email subject line.
comment	No	Body text for Email delivery.
(Any additional query parameters, like &Chain=GNG&Branch=GAAL, will be passed directly to the SSRS report).

Example Payloads
1. File Share Delivery (With Email Notification)
This payload triggers a report to be generated and saved to the default network file share. Because email_to is included, the API will poll SSRS in the background and send an email to user@aspial.com containing the exact file path once the PDF is successfully saved

{ 
  "url": "[https://dummy.com/?report_path=/LEGACY_IMS/MCJG/Trade%20in%20Report%20%28Summary%29/rptTradeinSummarybyCategory&delivery_method=FileShare&email_to=user@aspial.com&file_name=TradeInSummary&subject=rptTradeinSummarybyCategory&comment=Generated%20via%20API&Chain=GNG&Branch=GAAL&Branch=GABK&Branch=GACP&Branch=GAGY&Branch=GAHF&Method=ALL&CategoryType=Gold&CategoryType=Jew](https://dummy.com/?report_path=/LEGACY_IMS/MCJG/Trade%20in%20Report%20%28Summary%29/rptTradeinSummarybyCategory&delivery_method=FileShare&email_to=murshid_p@aspial.com&file_name=TradeInSummary&subject=rptTradeinSummarybyCategory&comment=Generated%20via%20API&Chain=GNG&Branch=GAAL&Branch=GABK&Branch=GACP&Branch=GAGY&Branch=GAHF&Method=ALL&CategoryType=Gold&CategoryType=Jew)" 
}
2. Direct Email Delivery
This payload bypasses the file share and instructs SSRS to email the generated report directly as an attachment.

{ 
  "url": "[https://dummy.com/?report_path=/LEGACY_IMS/MCJG/Trade%20in%20Report%20%28Summary%29/rptTradeinSummarybyCategory&delivery_method=Email&email_to=murshid_p%40aspial.com&subject=rptTradeinSummarybyCategory&comment=Generated%20via%20API&Chain=GNG&Branch=GAAL&Branch=GABK&Branch=GACP&Branch=GAGY&Branch=GAHF&Method=ALL&CategoryType=Gold&CategoryType=Jew](https://dummy.com/?report_path=/LEGACY_IMS/MCJG/Trade%20in%20Report%20%28Summary%29/rptTradeinSummarybyCategory&delivery_method=Email&email_to=murshid_p%40aspial.com&subject=rptTradeinSummarybyCategory&comment=Generated%20via%20API&Chain=GNG&Branch=GAAL&Branch=GABK&Branch=GACP&Branch=GAGY&Branch=GAHF&Method=ALL&CategoryType=Gold&CategoryType=Jew)" 
}

Background Polling: Long-running reports do not block HTTP traffic. The controller instantly returns a 200 OK response while spawning an IServiceScopeFactory thread to safely monitor SSRS job completion in the background.



