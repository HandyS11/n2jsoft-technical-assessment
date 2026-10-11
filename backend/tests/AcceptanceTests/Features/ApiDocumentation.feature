Feature: API documentation

  Scenario Outline: The v1 OpenAPI document describes the users endpoints
    Given the API runs in the Development environment
    When I request "/openapi/v1.json"
    Then the response status is 200
    And the OpenAPI document lists the path "<path>"

    Examples:
      | path           |
      | /v1/users      |
      | /v1/users/{id} |

  Scenario: The Scalar reference is served in Development
    Given the API runs in the Development environment
    When I request "/scalar/"
    Then the response status is 200

  Scenario: API documentation is not exposed outside Development
    When I request "/openapi/v1.json"
    Then the response is a problem with status 404
