# GymOS MVP Specification for Client Demonstration

> **Purpose**
>
> Build a polished MVP that demonstrates the complete vision of GymOS
> without requiring any existing client data, credentials, payment
> gateways, or third-party integrations. The MVP should be immediately
> demo-ready and designed so that real client data can be imported after
> project approval.

------------------------------------------------------------------------

# Product Goals

The MVP should convince a gym owner that:

-   The software is production quality.
-   Existing data can be migrated later.
-   Current workflows can be replaced.
-   The platform can scale to multiple branches.
-   Additional integrations can be enabled after approval.

------------------------------------------------------------------------

# Guiding Principles

-   No dependency on client credentials.
-   Use realistic demo data throughout.
-   Every module should feel complete.
-   Design for SaaS from day one.
-   Build import points before integrations.
-   Prioritize excellent UX over feature quantity.

------------------------------------------------------------------------

# Modules to Build

## 1. Executive Dashboard

Display: - Today's revenue - Cash balance - Active members - New
members - Expiring memberships - Attendance - Trainer schedule -
Equipment alerts - Maintenance reminders - Inventory alerts - Charts and
KPIs

------------------------------------------------------------------------

## 2. Authentication & RBAC

Roles: - Owner - Manager - Receptionist - Trainer - Nutritionist -
Accountant - Maintenance - Member

Features: - Login - Forgot password - MFA-ready - Session management -
Permission matrix

------------------------------------------------------------------------

## 3. Member Management

-   Registration
-   Member profile
-   Membership history
-   Medical notes
-   Emergency contacts
-   Measurements
-   Progress photos
-   QR membership card
-   Freeze/Renew/Transfer
-   Search & filters

------------------------------------------------------------------------

## 4. Membership Management

-   Monthly
-   Quarterly
-   Annual
-   Family
-   Corporate
-   Custom plans
-   Discounts
-   Coupons
-   Auto-renew flag
-   Expiry reminders

------------------------------------------------------------------------

## 5. CRM & Lead Management

Pipeline: Lead → Follow-up → Trial → Member → Renewal

Features: - Lead sources - Tasks - Follow-up reminders - Sales
dashboard - Conversion metrics

------------------------------------------------------------------------

## 6. Trainer Management

-   Trainer profile
-   Client assignment
-   Daily schedule
-   Availability
-   Performance dashboard
-   Commission tracking
-   Ratings

------------------------------------------------------------------------

## 7. Workout Management

-   Exercise library
-   Workout templates
-   Workout builder
-   Daily workout logs
-   Progress tracking

------------------------------------------------------------------------

## 8. Nutrition Module

-   Diet plans
-   Calories
-   Macros
-   Water tracking
-   Meal planner
-   Food library

------------------------------------------------------------------------

## 9. Attendance

-   QR check-in simulation
-   Manual check-in
-   Attendance history
-   Peak hour analytics
-   Visit frequency

------------------------------------------------------------------------

## 10. Billing & Invoicing

Without live gateways.

Support: - Cash - Card - Bank Transfer

Generate: - Invoice - Receipt - Outstanding balance - Refund record -
Payment reminders (simulation)

------------------------------------------------------------------------

## 11. Equipment Asset Management

Every asset contains: - Asset ID - QR Code - Photos - Manual -
Warranty - Supplier - Purchase details - Status - Service history -
Maintenance cost

------------------------------------------------------------------------

## 12. Maintenance (CMMS Lite)

-   Preventive maintenance
-   Corrective maintenance
-   Work orders
-   Technician assignment
-   Recurring reminders
-   Maintenance history
-   Downtime log

------------------------------------------------------------------------

## 13. Inventory

-   Stock in/out
-   Supplements
-   Merchandise
-   Cleaning supplies
-   Spare parts
-   Low-stock alerts
-   Purchase records

------------------------------------------------------------------------

## 14. Reports

-   Revenue
-   Attendance
-   Memberships
-   Trainer KPIs
-   Inventory
-   Equipment
-   Maintenance
-   Export PDF/Excel (placeholder)

------------------------------------------------------------------------

## 15. Notification Center

Show scheduled notifications: - Membership expiry - Maintenance -
Birthdays - Follow-ups - Low stock

No external messaging integration required.

------------------------------------------------------------------------

## 16. Settings

-   Gym profile
-   Logo
-   Currency
-   Timezone
-   Branches
-   Roles
-   System preferences

------------------------------------------------------------------------

# Demo Data

Create realistic demo data:

-   300 Members
-   20 Trainers
-   500 Attendance records
-   80 Equipment assets
-   100 Inventory items
-   50 Leads
-   100 Invoices
-   30 Maintenance jobs

------------------------------------------------------------------------

# Features Deferred Until Client Approval

## Integrations

-   SEPA Direct Debit
-   Mollie
-   Stripe
-   WhatsApp Business API
-   Email provider
-   SMS provider
-   Door access controllers
-   RFID/Fingerprint/Face Recognition
-   Apple Health
-   Google Fit
-   Garmin
-   Accounting software
-   POS hardware

------------------------------------------------------------------------

# Migration Center

Build a complete import framework.

Support: - CSV - Excel - JSON

Import Wizards: - Members - Trainers - Memberships - Equipment -
Attendance - Inventory - Payments

Include: - Preview - Validation - Duplicate detection - Rollback

------------------------------------------------------------------------

# Technical Requirements

Frontend: - React - TypeScript - Tailwind CSS

Backend: - ASP.NET Core Web API

Database: - PostgreSQL

Realtime: - SignalR

Authentication: - JWT

Storage: - S3 Compatible

Architecture: - Clean Architecture - DDD - SOLID - Repository Pattern -
Modular design

------------------------------------------------------------------------

# Deliverables

The MVP must be suitable for live demonstrations.

It should look and behave like a production system while using demo data
only.

Every external dependency must be abstracted behind interfaces so real
integrations can be added after client approval without changing
business logic.

The codebase should be enterprise-grade, maintainable, well documented,
and ready for future SaaS deployment.
