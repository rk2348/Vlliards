using System;
using UnityEditor;
using UnityEngine;
using Vlliards.Desktop;

public static class DesktopRulesChecks
{
    [MenuItem("Vlliards/Desktop/Verify Shot Rules")]
    public static void Run()
    {
        var noHit = new DesktopShotRules(1);
        Expect(noHit.EvaluateFoul() == ShotFoul.NoObjectBallContact, "Missing contact must be a foul.");

        var wrongFirst = new DesktopShotRules(2);
        wrongFirst.RecordFirstContact(3);
        wrongFirst.RecordFirstContact(2);
        wrongFirst.RecordObjectBallPocketed(9);
        Expect(wrongFirst.EvaluateFoul() == ShotFoul.WrongFirstBall && !wrongFirst.IsLegalNine,
            "A later correct contact cannot repair an illegal first hit or win the game.");

        var railBeforeContact = new DesktopShotRules(1);
        railBeforeContact.RecordRailContact();
        railBeforeContact.RecordFirstContact(1);
        Expect(railBeforeContact.EvaluateFoul() == ShotFoul.NoRailOrPocket, "Rail before contact does not satisfy the rail rule.");
        railBeforeContact.RecordRailContact();
        Expect(railBeforeContact.EvaluateFoul() == ShotFoul.None, "Legal contact followed by a rail must pass.");

        var combination = new DesktopShotRules(2);
        combination.RecordFirstContact(2);
        combination.RecordObjectBallPocketed(9);
        Expect(combination.IsLegalNine, "A legal combination into nine must win.");
        combination.RecordScratch();
        Expect(combination.EvaluateFoul() == ShotFoul.Scratch && !combination.IsLegalNine,
            "A scratch overrides a pocketed nine.");

        var independent = new DesktopShotRules(4);
        independent.RecordFirstContact(4);
        independent.RecordObjectBallPocketed(6);
        Expect(independent.EvaluateFoul() == ShotFoul.None && !independent.IsLegalNine,
            "Concurrent shots must retain independent first-contact and scratch state.");
        Debug.Log("DESKTOP_RULES PASS: no contact, first contact, rail timing, legal nine, scratch, independent shots.");
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
