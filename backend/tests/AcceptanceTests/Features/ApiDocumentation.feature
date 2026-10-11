Feature: API documentation

  Scenario Outline: The v1 OpenAPI document describes the users endpoints
    Given the API runs in the Development environment
    When I request "/openapi/v1.json"
    Then the response status is 200
    And the OpenAPI document lists the path "<path>"

    Examples:
      | path                      |
      | /v1/users                 |
      | /v1/users/{id}            |
      | /v1/users/{id}/activate   |
      | /v1/users/{id}/deactivate |

  Scenario Outline: The v1 OpenAPI document describes the error responses
    Given the API runs in the Development environment
    When I request "/openapi/v1.json"
    Then the OpenAPI document declares the response "<status>" for "<method>" on "<path>"

    Examples:
      | method | path                      | status |
      | get    | /v1/users                 | 400    |
      | get    | /v1/users/{id}            | 404    |
      | post   | /v1/users                 | 400    |
      | put    | /v1/users/{id}            | 400    |
      | put    | /v1/users/{id}            | 404    |
      | post   | /v1/users/{id}/activate   | 404    |
      | post   | /v1/users/{id}/deactivate | 404    |
      | delete | /v1/users/{id}            | 404    |

  Scenario: The Scalar reference is served in Development
    Given the API runs in the Development environment
    When I request "/scalar/"
    Then the response status is 200

  Scenario: API documentation is not exposed outside Development
    When I request "/openapi/v1.json"
    Then the response is a problem with status 404
