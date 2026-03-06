using LSUtils.ProcessSystem;
using NUnit.Framework;

namespace LSUtils.Tests.ProcessSystem;

[TestFixture]
public class InverterNodeTests {
    [Test]
    public void Inverter_SuccessChild_ShouldReturnFailure() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Inverter("invert", inv => inv
                .Handler("child", session => LSProcessResultStatus.SUCCESS))
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
    }

    [Test]
    public void Inverter_FailureChild_ShouldReturnSuccess() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Inverter("invert", inv => inv
                .Handler("child", session => LSProcessResultStatus.FAILURE))
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void Inverter_WaitingChild_ShouldPropagateWaiting() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Inverter("invert", inv => inv
                .Handler("child", session => LSProcessResultStatus.WAITING))
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
    }

    [Test]
    public void Inverter_DoubleInverter_ShouldRestoreOriginalResult() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Inverter("outer", outer => outer
                .Inverter("inner", inner => inner
                    .Handler("child", session => LSProcessResultStatus.SUCCESS)))
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }
}
