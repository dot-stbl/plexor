// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IKubectlCliRunner — abstracts kubectl shell-out for k3s runtime
// deployments. v0.1 implementation shells out to the host's kubectl
// pointing at the local k3s control-plane credentials
// (/etc/rancher/k3s/k3s.yaml — installed by k3s itself when the
// node joins a k3s cluster).
//
// Provisioning of the k3s cluster itself (install, join other
// nodes, etc.) is out of scope for the runtime-providers plan —
// that lives in plan-k8s. This provider assumes k3s is already
// installed and the kubeconfig is available on the node.
// ============================================================================

namespace Plexor.NodeAgent.Providers.Runtime;

/// <summary>
///     Shell-out to the host's kubectl CLI. Captures stdout,
///     throws on non-zero exit. The wrapper appends the k3s
///     kubeconfig path implicitly so call sites don't have to
///     repeat it on every invocation.
/// </summary>
public interface IKubectlCliRunner
{
    /// <summary>
    ///     Run <c>kubectl --kubeconfig=/etc/rancher/k3s/k3s.yaml
    ///     &lt;args&gt;</c> synchronously. Returns the captured
    ///     stdout (trimmed). Throws
    ///     <see cref="InvalidOperationException" /> with the
    ///     captured stderr when kubectl exits non-zero.
    /// </summary>
    /// <param name="args">Arguments passed to kubectl.</param>
    /// <param name="cancellationToken"></param>
    public Task<string> RunAsync(string args, CancellationToken cancellationToken);
}
