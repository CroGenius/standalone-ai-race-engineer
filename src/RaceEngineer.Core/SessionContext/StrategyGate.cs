using System.Globalization;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.Strategy;

namespace RaceEngineer.Core.SessionContext;

public static class StrategyGate
{
    public static SessionStrategy Apply(
        SessionStrategy raw,
        SessionContextAssessment context,
        SessionState? session = null)
    {
        if (context.Phase == SessionPhase.Review)
        {
            return raw with
            {
                CalloutSignal = null,
                StrategyConfidence = context.StrategyConfidence,
                StrategyConfidenceLabel = context.StrategyConfidenceLabel
            };
        }

        if (context.Activity is VehicleActivity.Stationary or VehicleActivity.PitLane)
        {
            return BuildStationaryStrategy(raw, context, session);
        }

        if (context.Phase is SessionPhase.Practice or SessionPhase.Qualifying or SessionPhase.Unknown)
        {
            return BuildNonRaceStrategy(raw, context, session);
        }

        if (context.StrategyConfidence == StrategyConfidenceLevel.Low || !context.HasStableLapSamples)
        {
            return BuildLowConfidenceStrategy(raw, context, session);
        }

        if (!context.AllowUnsolicitedStrategyCallouts)
        {
            return raw with
            {
                CalloutSignal = null,
                StrategyConfidence = context.StrategyConfidence,
                StrategyConfidenceLabel = context.StrategyConfidenceLabel
            };
        }

        return raw with
        {
            StrategyConfidence = context.StrategyConfidence,
            StrategyConfidenceLabel = context.StrategyConfidenceLabel
        };
    }

    private static SessionStrategy BuildStationaryStrategy(
        SessionStrategy raw,
        SessionContextAssessment context,
        SessionState? session)
    {
        return raw with
        {
            Fuel = raw.Fuel with
            {
                RiskLevel = FuelRiskLevel.Unknown,
                LapsRemaining = null,
                EstimatedFinishFuel = null,
                Availability = "Stationary — fuel level only."
            },
            Pit = raw.Pit with
            {
                Recommendation = PitRecommendation.Unknown,
                RecommendationReason = "Pit strategy paused while stationary.",
                Availability = "Unavailable while stationary."
            },
            TyreRisk = raw.TyreRisk with
            {
                RiskLevel = TyreRiskLevel.Unknown,
                Availability = "Unavailable while stationary."
            },
            Summary = $"{FormatFuelLevel(session)} Stationary — race fuel projections paused.",
            CalloutSignal = null,
            StrategyConfidence = StrategyConfidenceLevel.Low,
            StrategyConfidenceLabel = "Low"
        };
    }

    private static SessionStrategy BuildNonRaceStrategy(
        SessionStrategy raw,
        SessionContextAssessment context,
        SessionState? session)
    {
        var fuelRisk = context.HasStableLapSamples && raw.Fuel.RiskLevel != FuelRiskLevel.Unknown
            ? FuelRiskLevel.Low
            : FuelRiskLevel.Unknown;

        return raw with
        {
            Fuel = raw.Fuel with
            {
                RiskLevel = fuelRisk,
                EstimatedFinishFuel = null,
                Availability = context.HasStableLapSamples
                    ? "Practice/qualifying — fuel level only."
                    : raw.Fuel.Availability
            },
            Pit = raw.Pit with
            {
                Recommendation = PitRecommendation.StayOut,
                RecommendationReason = "Practice or qualifying — no race pit window active.",
                Availability = "Unavailable unless explicitly asked."
            },
            TyreRisk = context.AllowTyreWarningCallouts
                ? raw.TyreRisk
                : raw.TyreRisk with
                {
                    RiskLevel = TyreRiskLevel.Unknown,
                    Availability = "Practice/qualifying — tyre warnings suppressed."
                },
            Summary = context.HasStableLapSamples
                ? $"{FormatFuelLevel(session)} {context.SuppressionNote}"
                : "Not enough race data yet — waiting for stable lap samples.",
            CalloutSignal = null,
            StrategyConfidence = context.StrategyConfidence,
            StrategyConfidenceLabel = context.StrategyConfidenceLabel
        };
    }

    private static SessionStrategy BuildLowConfidenceStrategy(
        SessionStrategy raw,
        SessionContextAssessment context,
        SessionState? session)
    {
        return raw with
        {
            Fuel = raw.Fuel with
            {
                RiskLevel = FuelRiskLevel.Unknown,
                EstimatedFinishFuel = null,
                Availability = context.HasStableLapSamples ? raw.Fuel.Availability : "Waiting for stable lap samples."
            },
            Pit = raw.Pit with
            {
                Recommendation = PitRecommendation.Unknown,
                RecommendationReason = "Need stable lap samples before pit guidance.",
                Availability = "Waiting for stable lap samples."
            },
            TyreRisk = raw.TyreRisk with
            {
                RiskLevel = TyreRiskLevel.Unknown,
                Availability = "Waiting for stable lap samples."
            },
            Summary = context.HasStableLapSamples
                ? $"{FormatFuelLevel(session)} {context.SuppressionNote}"
                : "Not enough race data yet — waiting for stable lap samples.",
            CalloutSignal = null,
            StrategyConfidence = StrategyConfidenceLevel.Low,
            StrategyConfidenceLabel = "Low"
        };
    }

    private static string FormatFuelLevel(SessionState? session)
    {
        if (session?.LatestFuelLevel is { } fuel)
        {
            return $"Fuel {fuel.ToString("0.0", CultureInfo.InvariantCulture)} L.";
        }

        return "Fuel level unavailable.";
    }
}
