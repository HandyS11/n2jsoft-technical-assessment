Feature: Delete a user

  Scenario: A deleted user leaves the list but stays stored
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
      | Marc       | Assin     |
    When I delete the user "Juste Leblanc"
    Then the response status is 204
    And the user "Juste Leblanc" is still stored, marked as deleted
    When I list the users
    Then the users listed are
      | Name       |
      | Marc Assin |
    And the listing reports 1 users in total

  Scenario Outline: A deleted user can no longer be read or changed
    Given the following users exist
      | First name | Last name | Status  |
      | Juste      | Leblanc   | Deleted |
    When I <action> the user "Juste Leblanc"
    Then the response is a problem with status 404

    Examples:
      | action     |
      | get        |
      | update     |
      | activate   |
      | deactivate |
      | delete     |

  Scenario: Deleting an unknown user answers not found
    When I delete an unknown user
    Then the response is a problem with status 404
