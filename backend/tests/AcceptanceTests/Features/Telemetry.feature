Feature: Telemetry

  The API traces its requests down to the database, so the Aspire dashboard shows where the time goes.

  Scenario: A request is traced down to the database
    Given the API records its traces
    When I request "/v1/users"
    Then the request to "/v1/users" is traced as a server span with the status 200
    And the trace of the request to "/v1/users" contains a database span

  Scenario Outline: Health probes are not traced
    Given the API records its traces
    When I request "<path>"
    And I request "/v1/users"
    Then the request to "/v1/users" is traced as a server span with the status 200
    And no server span is recorded for the path "<path>"

    Examples:
      | path    |
      | /health |
      | /HEALTH |

  Scenario: The API keeps answering when its telemetry cannot be exported
    Given the API exports its telemetry to a collector that does not answer
    When I request "/v1/users"
    And the API has failed to export its telemetry
    And I request "/v1/users"
    Then the response status is 200
