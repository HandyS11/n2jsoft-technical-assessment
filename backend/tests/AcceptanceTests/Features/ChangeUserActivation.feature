Feature: Activate and deactivate a user

  Scenario: Deactivating an active user
    Given the following users exist
      | First name | Last name | Status |
      | Juste      | Leblanc   | Active |
    When I deactivate the user "Juste Leblanc"
    Then the response status is 204
    When I get the user "Juste Leblanc"
    Then the user returned has the status "Inactive"

  Scenario: Activating an inactive user
    Given the following users exist
      | First name | Last name | Status   |
      | Juste      | Leblanc   | Inactive |
    When I activate the user "Juste Leblanc"
    Then the response status is 204
    When I get the user "Juste Leblanc"
    Then the user returned has the status "Active"

  Scenario Outline: Asking for the status a user already has changes nothing
    Given the following users exist
      | First name | Last name | Status   |
      | Juste      | Leblanc   | <status> |
    When I <action> the user "Juste Leblanc"
    Then the response status is 204
    When I get the user "Juste Leblanc"
    Then the user returned has the status "<status>"

    Examples:
      | action     | status   |
      | activate   | Active   |
      | deactivate | Inactive |

  Scenario Outline: Changing the status of an unknown user answers not found
    When I <action> an unknown user
    Then the response is a problem with status 404

    Examples:
      | action     |
      | activate   |
      | deactivate |
