using NUnit.Framework;

namespace GripRehab.Editor.Tests
{
    // Unity Test Framework entry points for the existing native verifications (audit A-012),
    // so `Unity -runTests -testPlatform EditMode` reports them. Each verification throws on failure.
    public sealed class RehabEditModeTests
    {
        [Test] public void ExerciseDisplay() => RehabExerciseVerification.Run();
        [Test] public void WholeHandPointer() => RehabDwellVerification.Run();
        [Test] public void SceneComposition() => SceneOrganization.Verify();
        [Test] public void FatigueAiAndSentisParity() => FatigueAiVerification.Run();
        [Test] public void SqueezeLiquidAuthored() => SqueezeLiquidAuthoring.Verify();
    }
}
