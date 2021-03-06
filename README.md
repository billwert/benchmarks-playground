# What is this

Some basic benchmarks across different cloud-native stacks for testing performance under resource limits. I'm specifically investigating how different tech stacks behave when using CPU or memory limits in k8s (`--cpus` or `-m` in Docker) to configure applications for **density**.

There are other good benchmarks like TechEmpower that compare web stacks with the goal of maximum throughput with access to big hardware. I wanted to do my own benchmarking exercise that would give a different perspective focused on density and efficiency.

For this purpose of this testing, I'm making the assumption that you're invested in an orchestrator like kubernetes, and you want to partion you apps in such a way that kubernetes can dynamically scale them for you. Deciding the resource requirements of a single 'unit' of the application is a necessary step.

I'm also making the assumption that you want to oversubscribe your CPU resources. When applications need to handle a small and predictable amount of traffic, or go through quiet periods, there's an advantage to be had by packing many applications onto your hardware. CPU time is flexible and applications can contine to function, albeit slowly, even when there's less available than you want. Memory is *inflexible* - once memory is reserved for a container it will not be available for other applications. The Kubernetes docs refer to CPU as a *compressable* resource, and memory as a *non-compressable* resource.

By this measure, it's valuable to oversubscribe CPU time - other applications can use and benefit from any surplus. It's valuable to configure your applications to be frugal with memory - because it can't be shared and redistributed in the same way.

---

From the point of view of an ASP.NET Core framework/platform developer, I'm tying to do an exercise that will help understand:

- How is ASP.NET Core positioned vs other mainstream cloud-native web stacks?
- What guidance should we give ASP.NET Core developers that want to optimize for density?
- What gaps does .NET/ASP.NET Core have that the team should address?

## What does the app do

The app is a tiny REST API. It serves a really small JSOM payload. It also makes an outgoing HTTP call to another application, and does some JSON deserialization.

Why do all of this? This set of things is designed to include features that are critical to the microservices programming style (for REST/JSON).

The app that we're testing (weatherapp) does:

- Gets and HTTP request
- Does an outgoing HTTP call to another app (forecast app)
- Deserializing the JSON that comes back
- Serializes JSON to its response

Our *other app* (forecastapp) in this cast serves a JSON payload with a small delay. The reason for the delay is that we want to see some queueing and expose the effects of the underlying platform's threading model. The vast majority of microservices workflows will involve talking to some data data source. Forecast app is set up so that it won't be the bottleneck in our testing. It's written at a pretty low level and is allocated *much* more resources in the testing environment.

Why not build a more realistic app, with real database? Well, increasing the size and scope of what's being tested decreases the chance that we get something wrong. The quality of database drivers varies a lot, and different programming communities prefer different databases. It would be much most significant of a task to make sure that we're comparing the right things.

## How I wrote these

I'm trying to understand where you land by writing applications using various **mainstream** tech stacks and using **mainstream** practices. If your team had to write 30 services in stack XYZ how would you write them? I specifically want to avoid doing anything esoteric in the code, or doing lots of scenario-specific tuning in the app/config. I want these to honestly reflect the common practices.

[Techempower](https://www.techempower.com/benchmarks/) exists to serve as a benchmarking competion, the code samples here are intended to track realistic and straightforward code samples.

## What scenarios are tested

Forwcast app is tested with a spectrum of CPU x memory combinations, and a spectrun of fixed-requests-per-second load values, measuring:

- CPU usage
- memory consumption
- latency
- swap

If you were doing this kind of analysis in production, you might pick an expected value of RPS, and then choose a CPU x memory combination that gives latency within SLA.

We're tracking swap in this these cases to find combinations of CPU x memory x RPS that require usages of swap space. This is a red flag for performance, because swap does disk i/o, and that's disk i/o that could be avoided by giving the application more memory. Relying on swapping to make the application function is not going to achieve our goal of achieving density. In our test environment we're only running two applications - in a production configuration there will be many other applications contending for use of the disk.

---

As for the actual combinations of CPU and memory, these are informed by:

- Observation of requests/limits used by kubernetes infrastructure
- SKUs of Azure VM available and their ratios
- Reasoning about our goal of density

---

Looking through the `kube-system` namespace you can find containers that request as low as `10m` CPU(`--cpus="0.01"`) and `10mi` memory. You'll also see cases where CPU limits are as low as `1.0` and memory limits are less than `200mi`.

We can draw a few conclusions from this:

- Clearly there's a bit of a range in terms of what's allocated to these infrastructure bits, so we're going to test a range.
- The target resource requests/limits for an infrastructure piece (kubernetes operator, daemon, or webhook) is going to be lower than a traditional app, because the scalability expectations are lower.
- This is a case where you generally want to optimize for efficiency over raw throughput.

note: For the purpose of eliminating some subtlety, I'm not making a distinction in testing between *requests* and *limits*. For a piece of infrastructure you want to make your requests as low as possible - instead of trying to design experiments around requests, we're making the assumption that you might need to run with *only* the requested amount available, and limits are an effective way to test that.

---

Doing analysis across [Azure's VM offerings](https://docs.microsoft.com/en-us/azure/virtual-machines/linux/sizes?toc=%2fazure%2fvirtual-machines%2flinux%2ftoc.json) should help us understand what resources are available and in what ratios. Different VM skus offer different tradeoffs, ratios, and pricing, so it makes sense that users would want to accept different tradeoffs for different apps.

VM size/groups that seemed relevant as *examplars* for this analysis were the A, B, D, and F. From examining these VMs we can determine what combinations make most sense to test.

note: This is a reminder that I'm not that interested in testing what happens when you allocate a really large amount of resources to a single instance.

### General Purpose

The [D-series](https://docs.microsoft.com/en-us/azure/virtual-machines/linux/sizes-general) (all of them) are the standard "general purpose" VM. The various options within the -series offer different choices of processor.

**CPU/Memory ratio:** 1 CPU / 3.5 GiB - 1 CPU / 4 GiB

**Size range in CPUs:** 1 - 96 CPUs

### General Purpose - non-hardware-specific

The [Av2-series](https://docs.microsoft.com/en-us/azure/virtual-machines/linux/sizes-general#av2-series) doesn't provide any guarantees about what physical hardware you're running upon. The advantage of this is in [the price](https://azure.microsoft.com/en-us/pricing/details/virtual-machines/linux/). For instance an A2v2 comes in at $0.076/hour where a comparable F2s2v is $0.085/hour.

The Azure docs mention that this VM series is most suitable for test/staging environments or low traffic web servers.

**CPU/Memory ratio:** 1 CPU / 2 GiB - 1 CPU / 8 GiB

**Size range in CPUs:** 1 - 8 CPUs

### General Purpose - bustable

The [B-series](https://docs.microsoft.com/en-us/azure/virtual-machines/linux/sizes-general#b-series) is a VM choice optimized for workloads that have bursty workloads. B-series VMs are cost effective when the application's CPU usage is low and predictable most of the time.

**CPU/Memory ratio:** 1 CPU / 0.5 GiB - 1 CPU / 4 GiB

**Size range in CPUs:** 1 - 20 CPUs

note: The B-series VM is optimized for a usage ratio like 0.1 CPU / 1 GiB in non-burst mode. This is a valuable point of comparison for testing workloads with predicatable RPS - how well does an app fit into the non-burst requirements of the B-series.

### Compute Optimized

Azure's docs describe the [Fsv2-series](https://docs.microsoft.com/en-us/azure/virtual-machines/linux/sizes-compute#fsv2-series-1) as the "compute optimized" choice - the most efficient way to pay for access to cores if that's you're limiting resource.

**CPU/Memory ratio:** 1 CPU / 2 GiB

**Size range in CPUs:** 2 - 72 CPUs

### Conclusions

Looking across all of the VM types considered, the CPU / memory ratios range from `1 CPU / 2 GiB` to `1 CPU / 4GiB`. Given that we're after density, we want to consider ratios with a lower amount of memory than what's given by a typical VM. We're testing oversubscription of CPUs relative to memory.

Additionally, in these configurations AKS will resevere about 35% for a very small VM, and that percentage will decrease as the size of the VM's memory pool increases. So while we also want to oversubscribe based on these ratios we should also be aware that the OS and k8s will take some up as well.

I'm choosing a range of CPU values from `0.25`, `0.5`, `1.0` since this focuses on oversubscription. I'm going choose memory values that roughly correspond to the ratios `1 CPU / .125 GiB`, `1 CPU / .25 GiB`, `1 CPU / .375 GiB`.

**Combinations to test (CPU x Memory):**

- `0.25 x 30m`
- `0.25 x 50m`
- `0.25 x 90m`
- `0.5 x 60m`
- `0.5 x 100m`
- `0.5 x 180m`
- `1.0 x 120m`
- `1.0 x 200m`
- `1.0 x 360`

## How is this tested

I'm using the ASP.NET Core benchmarking infrastructure: [aspnet/Benchmarks](https://github.com/aspnet/Benchmarks). The tools and infrastructure are OSS (anyone can deploy this), however access to our servers running it are not.

For the unfamiliar, our benchmarking tools run as a server, which can assign server jobs (running an application) and client jobs (running a load testing tool) to other computers. Our setup is used regularly to benchmark workloads that run millions of requests-per-second.

Our benchmarking system can run apps in a variety of .NET-centric ways as well as with containers. We're exclusively using containers for this test.

Our benchmarking system has wired up a bunch of well-known load-generation clients (`wrk`, `wrk2`, Bombardier), as well as some of our own invention.

### Procedure: Testing

We can use the `wrk2` client because it provides both the ability to test with fixed requests-per-second, and has a very detailed report of latencies. We use this client to test workloads that generate load in the millions of requests in our lab setup, it will easily reach the levels of load we require without becoming a bottleneck.

A typical trial for testing connections looks like:

- Deploy app
- Run a 30s warmup
- Run a 15s *test run*

We're using a 30 second warmup to be conservative, by the time the *test run* starts, a .NET or Java app will have already served thousands of requests even if it's only handling a hundred per second.

We're testing the following values of requests-per-second across of all of these combinations. We don't expect every app to successfully handle all of these load values in every combination.

- 1 RPS
- 100 RPS
- 500 RPS
- 1000 RPS
- 10000 RPS
- 25000 RPS

## FAQ

**Q: Using --cpus=1.0 is going to have bad performance in my stack of choice because it will prevent the GC from running in parallel with user code.**

**A:** This is a misunderstanding of what `--cpus` does. Docker has a [*variety*](https://docs.docker.com/config/containers/resource_constraints/#cpu) of cpu-related settings.

`--cpus` limits the proportion of CPU time available to the app. If the maching has 12 logical CPUs, then setting `--cpus=1.0` means the application gets 1/12 of the available CPU time aggregated across some quantum of time. This does not make the app "single threaded" - this is no correspondence between `--cpus` and the *effective concurrency* of the app.

**Q: My stack of choice has better frameworks/runtimes/options that are more optimized for this use-case.**

**A:** PRs welcome :) as are independent validations of my results.

The question in my mind when you give this feedback is - are these other options *mainstream choices?* Would a normal developer in a normal workplace adopt this solution in production? Does the technology maker say that it's ready for production use?

**Q: You're doing something suboptimal or inefficient. Plz fix.**

**A:** Please point out the error, or a send a PR so I can fix it and collect more data. However, I'm going to ask for some kind of documentation or guidance about this practice and why it's wrong (could be a blog article, or a github issue somewhere other than my repo).

Remember that my stated goal is to capture the experiences that mainstream users will have. I'm not logging to micro-optimize all of these samples, and I'm trying to stick to the defaults where the defaults are reasonable.

An example of this would be ineffiecient use of `HttpClient` in .NET. Application authors have to have a strategy to avoid over-allocating `HttpClient` instances. `RestTemplate` in Spring Boot has similar issues. If I'm making this kinds of mistakes then I want to fix them.

---

The kind of thing I won't compromise on would be something like changing `JsonSerializer.Serialize(...)` to write a hardcoded set of bytes to the output.

In many ways a benchmark is always a facsimile of a real application. I'm going to keep the idiomatic patterns of a real application in place to maintain that connection.

---

Ultimately this is my repo, my goals, my time spent collecting data, and my credibility associated with any conclusions I draw - so every judgement call (and there are a lot of them) on every decision. If you think I'm wrong there's no better case to make than one backed up with data.

**Q: Why doesn't this have a database?**

**A:** All benchmarks are imperfect, and you have to draw the line somewhere when making comparisons across tech stacks.

This seemed like a good place to draw the line. Different tech stacks have different affinities to different databases as well. I don't want to increase the surface area of what I'm testing more than necessary.
