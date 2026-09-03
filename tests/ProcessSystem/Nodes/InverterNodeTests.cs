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
                .Handler("child", session => LSProcessResult.Success))
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(LSProcessResult.Failure));
    }

    [Test]
    public void Inverter_FailureChild_ShouldReturnSuccess() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Inverter("invert", inv => inv
                .Handler("child", session => LSProcessResult.Failure))
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(LSProcessResult.Success));
    }

    [Test]
    public void Inverter_WaitingChild_ShouldPropagateWaiting() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Inverter("invert", inv => inv
                .Handler("child", session => LSProcessResult.Waiting))
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(LSProcessResult.Waiting));
    }

    [Test]
    public void Inverter_DoubleInverter_ShouldRestoreOriginalResult() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Inverter("outer", outer => outer
                .Inverter("inner", inner => inner
                    .Handler("child", session => LSProcessResult.Success)))
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(LSProcessResult.Success));
    }
}
