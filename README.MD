# Synopsys
Not all project implements optimal architecture, and derogation of health is not uncommon among long lived projects.
Database thrashing is a usual bottleneck, where many queries/transactions are made in a single executing unit of 
business logic. These business logic units are typically monolithic in nature or intimidating to refactor.

The purpose of this project is to simplify the process of applying a common strategy to optimize said units.
Specifically, the solution that applies for said cases is to batch the plurality of single request to the database 
into a flat unit of execution, whereby vertical depth progression of the sub optimal unit is deferred in favor of
a batched unit that spans horizontally over the data of interest.

# Requirements
Due to the intimidating nature of applicable projects, one may wish to avoid altering code that defines the complex
unit of business logic that has degraded over time, or is in such a state due to its intrinsic properties. To simplify
the process, there must be a mechanism that enables a developer to add batching functionality in place, without having
to move a significant amount of code around. If suboptimal performance is not due to frequent round trips, then this
project is not applicable.