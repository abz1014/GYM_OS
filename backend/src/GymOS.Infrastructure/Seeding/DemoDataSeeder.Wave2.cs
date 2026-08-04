using GymOS.Domain.Crm;
using GymOS.Domain.Equipment;
using GymOS.Domain.Identity;
using GymOS.Domain.Inventory;
using GymOS.Domain.Maintenance;
using GymOS.Domain.Members;
using GymOS.Domain.Notifications;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Trainers;
using GymOS.Shared;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Infrastructure.Seeding;

public partial class DemoDataSeeder
{
    private async Task<List<Trainer>> SeedTrainersAsync(Guid tenantId, List<Branch> branches, Dictionary<string, User> demoUsers, CancellationToken cancellationToken)
    {
        var rng = new Random(4004);
        var trainers = new List<Trainer>();
        var trainerRole = await db.Roles.IgnoreQueryFilters().FirstAsync(r => r.TenantId == tenantId && r.Name == RoleNames.Trainer, cancellationToken);
        var specialties = new[] { "Strength Training", "Cardio & HIIT", "Yoga", "CrossFit", "Bodybuilding", "Functional Fitness", "Pilates" };

        for (var i = 0; i < 20; i++)
        {
            User user;
            if (i == 0)
            {
                user = demoUsers[RoleNames.Trainer];
            }
            else
            {
                var firstName = _faker.Name.FirstName();
                var lastName = _faker.Name.LastName();
                user = new User
                {
                    TenantId = tenantId,
                    Email = _faker.Internet.Email(firstName, lastName, "titanfitness.demo").ToLowerInvariant(),
                    PasswordHash = passwordHasher.Hash(DemoPassword),
                    FirstName = firstName,
                    LastName = lastName,
                    IsActive = true
                };
                db.Users.Add(user);
                await db.SaveChangesAsync(cancellationToken);

                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = trainerRole.Id });
                db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branches[rng.Next(branches.Count)].Id });
            }

            var branch = branches[rng.Next(branches.Count)];

            var trainer = new Trainer
            {
                TenantId = tenantId,
                BranchId = branch.Id,
                UserId = user.Id,
                Specialties = _faker.PickRandom(specialties, rng.Next(1, 3)).Distinct().Aggregate((a, b) => $"{a}, {b}"),
                CommissionRate = Math.Round(5 + rng.NextDouble() * 10, 1).ToDecimalSafe(),
                Bio = _faker.Lorem.Sentence(12),
                IsActive = true
            };

            foreach (var dayOfWeek in new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday })
            {
                db.TrainerSchedules.Add(new TrainerSchedule
                {
                    Trainer = trainer,
                    DayOfWeek = dayOfWeek,
                    StartTime = new TimeOnly(rng.Next(6, 10), 0),
                    EndTime = new TimeOnly(rng.Next(15, 19), 0),
                    IsAvailable = true
                });
            }

            db.Trainers.Add(trainer);
            trainers.Add(trainer);
        }

        await db.SaveChangesAsync(cancellationToken);
        return trainers;
    }

    private async Task SeedTrainerAssignmentsAsync(List<Trainer> trainers, List<Member> members, CancellationToken cancellationToken)
    {
        var rng = new Random(5005);
        var eligibleMembers = members.Where(m => m.Status == MemberStatus.Active).ToList();

        foreach (var trainer in trainers)
        {
            var assignedCount = rng.Next(5, 16);
            foreach (var member in eligibleMembers.OrderBy(_ => rng.Next()).Take(assignedCount))
            {
                db.TrainerAssignments.Add(new TrainerAssignment
                {
                    TrainerId = trainer.Id,
                    MemberId = member.Id,
                    StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-rng.Next(10, 200)),
                    IsActive = true
                });
            }

            foreach (var _ in Enumerable.Range(0, rng.Next(2, 6)))
            {
                db.TrainerRatings.Add(new TrainerRating
                {
                    TrainerId = trainer.Id,
                    MemberId = eligibleMembers[rng.Next(eligibleMembers.Count)].Id,
                    Score = rng.Next(3, 6),
                    Comment = _faker.PickRandom("Great trainer!", "Really pushed me to improve.", "Very knowledgeable.", "Friendly and motivating.", null),
                    RatedAt = DateTimeOffset.UtcNow.AddDays(-rng.Next(1, 120))
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<Asset>> SeedEquipmentAsync(Guid tenantId, List<Branch> branches, CancellationToken cancellationToken)
    {
        var rng = new Random(6006);
        var categories = new[] { "Cardio", "Strength", "Free Weights", "Functional" };
        var equipmentNames = new[]
        {
            "Treadmill", "Elliptical Trainer", "Stationary Bike", "Rowing Machine", "Leg Press",
            "Cable Crossover", "Smith Machine", "Lat Pulldown", "Chest Press", "Dumbbell Set",
            "Barbell Rack", "Kettlebell Set", "Battle Ropes", "Squat Rack", "Bench Press"
        };
        var suppliers = new List<Supplier>();
        foreach (var name in new[] { "IronCore Equipment Co.", "FitPro Supply", "PeakForm Fitness Gear" })
        {
            var supplier = new Supplier { TenantId = tenantId, Name = name, ContactName = _faker.Name.FullName(), Phone = _faker.Phone.PhoneNumber(), Email = _faker.Internet.Email() };
            db.Suppliers.Add(supplier);
            suppliers.Add(supplier);
        }

        await db.SaveChangesAsync(cancellationToken);

        var assets = new List<Asset>();
        for (var i = 1; i <= 80; i++)
        {
            var branch = branches[rng.Next(branches.Count)];
            var purchaseDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-rng.Next(60, 1500));
            var statusRoll = rng.Next(100);

            var asset = new Asset
            {
                TenantId = tenantId,
                BranchId = branch.Id,
                AssetTag = $"EQ-{i:D4}",
                Name = equipmentNames[rng.Next(equipmentNames.Length)],
                Category = categories[rng.Next(categories.Length)],
                QrCodeToken = Guid.NewGuid().ToString("N"),
                SupplierId = suppliers[rng.Next(suppliers.Count)].Id,
                Status = statusRoll switch
                {
                    < 80 => AssetStatus.Active,
                    < 92 => AssetStatus.UnderMaintenance,
                    < 98 => AssetStatus.OutOfService,
                    _ => AssetStatus.Retired
                },
                PurchaseDate = purchaseDate,
                PurchasePrice = Math.Round(500 + rng.NextDouble() * 4500, 2).ToDecimalSafe(),
                WarrantyExpiresAt = purchaseDate.AddYears(rng.Next(1, 4))
            };

            db.Assets.Add(asset);
            assets.Add(asset);
        }

        await db.SaveChangesAsync(cancellationToken);
        return assets;
    }

    private async Task SeedMaintenanceAsync(Guid tenantId, List<Branch> branches, List<Asset> assets, Dictionary<string, User> demoUsers, CancellationToken cancellationToken)
    {
        var rng = new Random(7007);
        var maintenanceUser = demoUsers[RoleNames.Maintenance];

        for (var i = 1; i <= 30; i++)
        {
            var asset = assets[rng.Next(assets.Count)];
            var type = rng.Next(2) == 0 ? WorkOrderType.Preventive : WorkOrderType.Corrective;
            var statusRoll = rng.Next(100);
            var status = statusRoll switch
            {
                < 40 => WorkOrderStatus.Open,
                < 60 => WorkOrderStatus.InProgress,
                < 95 => WorkOrderStatus.Completed,
                _ => WorkOrderStatus.Cancelled
            };

            // A third of open/in-progress work orders are scheduled in the past (overdue), so the
            // dashboard/maintenance alerts widget has something real to flag.
            var scheduledDate = status is WorkOrderStatus.Open or WorkOrderStatus.InProgress && rng.Next(100) < 33
                ? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-rng.Next(1, 14))
                : DateOnly.FromDateTime(DateTime.UtcNow).AddDays(rng.Next(-5, 21));

            var workOrder = new WorkOrder
            {
                TenantId = tenantId,
                BranchId = asset.BranchId,
                AssetId = asset.Id,
                Type = type,
                Priority = _faker.PickRandom(WorkOrderPriority.Low, WorkOrderPriority.Medium, WorkOrderPriority.High, WorkOrderPriority.Critical),
                Status = status,
                Title = type == WorkOrderType.Preventive ? $"Scheduled maintenance — {asset.Name}" : $"Repair needed — {asset.Name}",
                Description = _faker.Lorem.Sentence(10),
                AssignedToUserId = maintenanceUser.Id,
                ScheduledDate = scheduledDate,
                CompletedDate = status == WorkOrderStatus.Completed ? scheduledDate.AddDays(rng.Next(0, 3)) : null,
                Cost = status == WorkOrderStatus.Completed ? Math.Round(20 + rng.NextDouble() * 300, 2).ToDecimalSafe() : null
            };

            db.WorkOrders.Add(workOrder);

            if (type == WorkOrderType.Corrective)
            {
                db.DowntimeLogs.Add(new DowntimeLog
                {
                    AssetId = asset.Id,
                    WorkOrder = workOrder,
                    StartedAt = scheduledDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    EndedAt = status == WorkOrderStatus.Completed ? scheduledDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(rng.Next(2, 48)) : null,
                    Reason = workOrder.Title
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedInventoryAsync(Guid tenantId, List<Branch> branches, CancellationToken cancellationToken)
    {
        var rng = new Random(8008);
        var itemsByCategory = new Dictionary<InventoryCategory, string[]>
        {
            [InventoryCategory.Supplement] = ["Whey Protein 2lb", "BCAA Powder", "Pre-Workout Mix", "Creatine Monohydrate", "Multivitamin"],
            [InventoryCategory.Merchandise] = ["Gym T-Shirt", "Water Bottle", "Gym Towel", "Lifting Gloves", "Resistance Band Set"],
            [InventoryCategory.CleaningSupply] = ["Disinfectant Spray", "Paper Towels", "Equipment Wipes", "Hand Sanitizer", "Mop Heads"],
            [InventoryCategory.SparePart] = ["Treadmill Belt", "Cable Wire", "Resistance Pin", "Bike Pedal", "Bolt & Screw Kit"]
        };

        var i = 0;
        foreach (var (category, names) in itemsByCategory)
        {
            foreach (var name in names)
            {
                for (var variant = 1; variant <= 5; variant++)
                {
                    i++;
                    var reorderLevel = rng.Next(5, 20);
                    var isLowStock = i % 7 == 0;
                    var quantity = isLowStock ? rng.Next(0, reorderLevel) : reorderLevel + rng.Next(5, 100);

                    db.InventoryItems.Add(new InventoryItem
                    {
                        TenantId = tenantId,
                        BranchId = branches[rng.Next(branches.Count)].Id,
                        Sku = $"SKU-{i:D4}",
                        Name = variant == 1 ? name : $"{name} (Batch {variant})",
                        Category = category,
                        QuantityOnHand = quantity,
                        ReorderLevel = reorderLevel,
                        UnitCost = Math.Round(2 + rng.NextDouble() * 30, 2).ToDecimalSafe(),
                        UnitPrice = Math.Round(5 + rng.NextDouble() * 50, 2).ToDecimalSafe()
                    });

                    if (i >= 100)
                    {
                        break;
                    }
                }

                if (i >= 100)
                {
                    break;
                }
            }

            if (i >= 100)
            {
                break;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedLeadsAsync(Guid tenantId, List<Branch> branches, Dictionary<string, User> demoUsers, CancellationToken cancellationToken)
    {
        var rng = new Random(9009);
        var assignees = new[] { demoUsers[RoleNames.Receptionist].Id, demoUsers[RoleNames.Manager].Id };

        for (var i = 0; i < 50; i++)
        {
            var firstName = _faker.Name.FirstName();
            var lastName = _faker.Name.LastName();
            var stageRoll = rng.Next(100);

            var lead = new Lead
            {
                TenantId = tenantId,
                BranchId = branches[rng.Next(branches.Count)].Id,
                FirstName = firstName,
                LastName = lastName,
                Email = _faker.Internet.Email(firstName, lastName).ToLowerInvariant(),
                Phone = _faker.Phone.PhoneNumber("(###) ###-####"),
                Source = _faker.PickRandom<LeadSource>(),
                Stage = stageRoll switch
                {
                    < 30 => LeadStage.Lead,
                    < 55 => LeadStage.FollowUp,
                    < 75 => LeadStage.Trial,
                    < 90 => LeadStage.Member,
                    _ => LeadStage.Lost
                },
                AssignedToUserId = assignees[rng.Next(assignees.Length)]
            };

            db.Leads.Add(lead);

            db.LeadActivities.Add(new LeadActivity
            {
                Lead = lead,
                Type = _faker.PickRandom<LeadActivityType>(),
                Notes = _faker.Lorem.Sentence(8),
                DueDate = DateTimeOffset.UtcNow.AddDays(rng.Next(-5, 14)),
                CreatedByUserId = lead.AssignedToUserId
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedNotificationTemplatesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        db.NotificationTemplates.AddRange(
            new NotificationTemplate
            {
                TenantId = tenantId, Code = "membership-expiry-7-days", Category = NotificationCategory.MembershipExpiry,
                Channel = NotificationChannel.Email, Subject = "Your membership is expiring soon",
                BodyTemplate = "Hi {{FirstName}}, your membership expires on {{ExpiryDate}}. Renew now to keep your access!"
            },
            new NotificationTemplate
            {
                TenantId = tenantId, Code = "maintenance-due", Category = NotificationCategory.Maintenance,
                Channel = NotificationChannel.Email, Subject = "Maintenance due",
                BodyTemplate = "Equipment {{AssetName}} is due for maintenance."
            },
            new NotificationTemplate
            {
                TenantId = tenantId, Code = "birthday", Category = NotificationCategory.Birthday,
                Channel = NotificationChannel.Email, Subject = "Happy Birthday!",
                BodyTemplate = "Happy Birthday {{FirstName}}! Enjoy a free protein shake on us today."
            },
            new NotificationTemplate
            {
                TenantId = tenantId, Code = "follow-up-reminder", Category = NotificationCategory.FollowUp,
                Channel = NotificationChannel.Email, Subject = "Follow up reminder",
                BodyTemplate = "Reminder to follow up with lead {{FirstName}} {{LastName}}."
            },
            new NotificationTemplate
            {
                TenantId = tenantId, Code = "low-stock", Category = NotificationCategory.LowStock,
                Channel = NotificationChannel.Email, Subject = "Low stock alert",
                BodyTemplate = "{{ItemName}} is running low ({{QuantityOnHand}} remaining)."
            },
            new NotificationTemplate
            {
                TenantId = tenantId, Code = "payment-failed", Category = NotificationCategory.PaymentFailed,
                Channel = NotificationChannel.Email, Subject = "We couldn't process your payment",
                BodyTemplate = "Hi {{FirstName}}, your membership renewal payment didn't go through. We'll try again shortly — please check your payment details to avoid losing access."
            },
            new NotificationTemplate
            {
                TenantId = tenantId, Code = "churn-risk-winback", Category = NotificationCategory.ChurnRisk,
                Channel = NotificationChannel.Email, Subject = "We miss you at the gym!",
                BodyTemplate = "Hi {{FirstName}}, we haven't seen you in a while. Your membership is still active — come back this week and pick up where you left off."
            },
            new NotificationTemplate
            {
                TenantId = tenantId, Code = "class-reminder", Category = NotificationCategory.ClassReminder,
                Channel = NotificationChannel.Email, Subject = "Your class is coming up",
                BodyTemplate = "Hi {{FirstName}}, reminder: {{ClassName}} starts {{StartsAt}}. See you there!"
            });

        await db.SaveChangesAsync(cancellationToken);
    }
}
